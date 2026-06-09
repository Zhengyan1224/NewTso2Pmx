using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using NewTso2Pmx.Core.Preview;
using Silk.NET.OpenGL;

namespace NewTso2Pmx.Renderer;

public sealed class SilkPreviewControl : OpenGlControlBase
{
    public static readonly StyledProperty<PreviewSceneData?> SceneProperty =
        AvaloniaProperty.Register<SilkPreviewControl, PreviewSceneData?>(nameof(Scene));

    private GL? _gl;
    private uint _program;
    private uint _vertexArray;
    private uint _vertexBuffer;
    private uint _indexBuffer;
    private int _mvpLocation = -1;
    private int _textureLocation = -1;
    private int _useTextureLocation = -1;
    private bool _sceneDirty = true;
    private PreviewSceneData? _uploadedScene;
    private readonly Dictionary<PreviewTextureData, uint> _textures = new();
    private float _yaw = -0.8f;
    private float _pitch = 0.35f;
    private float _distance = 40.0f;
    private Vector3 _targetOffset = Vector3.Zero;
    private bool _isRotating;
    private bool _isPanning;
    private bool _isZooming;
    private Point _lastPointerPosition;
    private TaskCompletionSource<PreviewFrameCapture?>? _pendingCapture;

    public SilkPreviewControl()
    {
        Focusable = true;
    }

    public PreviewSceneData? Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    public Task<PreviewFrameCapture?> CaptureFrameAsync()
    {
        if (_gl is null)
        {
            return Task.FromResult<PreviewFrameCapture?>(null);
        }

        if (_pendingCapture is not null)
        {
            return _pendingCapture.Task;
        }

        _pendingCapture = new TaskCompletionSource<PreviewFrameCapture?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        RequestNextFrameRendering();
        return _pendingCapture.Task;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SceneProperty)
        {
            _sceneDirty = true;
            UpdateCameraFromScene(Scene);
            RequestNextFrameRendering();
        }
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        _gl = GL.GetApi(name => gl.GetProcAddress(name));
        _program = CreateProgram(_gl);
        _mvpLocation = _gl.GetUniformLocation(_program, "uMvp");
        _textureLocation = _gl.GetUniformLocation(_program, "uColorTexture");
        _useTextureLocation = _gl.GetUniformLocation(_program, "uUseTexture");
        _vertexArray = _gl.GenVertexArray();
        _vertexBuffer = _gl.GenBuffer();
        _indexBuffer = _gl.GenBuffer();
        _sceneDirty = true;
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        if (_gl is null)
        {
            return;
        }

        if (_indexBuffer != 0)
        {
            _gl.DeleteBuffer(_indexBuffer);
            _indexBuffer = 0;
        }

        if (_vertexBuffer != 0)
        {
            _gl.DeleteBuffer(_vertexBuffer);
            _vertexBuffer = 0;
        }

        if (_vertexArray != 0)
        {
            _gl.DeleteVertexArray(_vertexArray);
            _vertexArray = 0;
        }

        DeleteTextures();

        if (_program != 0)
        {
            _gl.DeleteProgram(_program);
            _program = 0;
        }

        _gl.Dispose();
        _gl = null;
    }

    protected override unsafe void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_gl is null)
        {
            return;
        }

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)fb);
        _gl.Viewport(0, 0, (uint)Math.Max(1, (int)Bounds.Width), (uint)Math.Max(1, (int)Bounds.Height));
        _gl.Enable(GLEnum.DepthTest);
        _gl.Enable(GLEnum.CullFace);
        _gl.CullFace(GLEnum.Back);
        _gl.Enable(GLEnum.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.ClearColor(0.88f, 0.90f, 0.94f, 1.0f);
        _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

        if (_sceneDirty)
        {
            UploadScene();
        }

        if (_uploadedScene is null || _uploadedScene.IsEmpty || _program == 0)
        {
            CompletePendingCapture();
            return;
        }

        var aspect = (float)Math.Max(0.2, Bounds.Width / Math.Max(1.0, Bounds.Height));
        var target = _uploadedScene.Center + _targetOffset;
        var cameraOffset = new Vector3(
            _distance * MathF.Cos(_pitch) * MathF.Sin(_yaw),
            _distance * MathF.Sin(_pitch),
            _distance * MathF.Cos(_pitch) * MathF.Cos(_yaw));
        var eye = target + cameraOffset;
        var view = Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4.0f, aspect, 0.1f, 10000.0f);
        var mvp = view * projection;

        _gl.UseProgram(_program);
        _gl.BindVertexArray(_vertexArray);
        _gl.UniformMatrix4(_mvpLocation, 1, false, (float*)&mvp);
        _gl.Uniform1(_textureLocation, 0);

        if (_uploadedScene.DrawBatches.Count == 0)
        {
            _gl.Uniform1(_useTextureLocation, 0);
            _gl.DrawElements(PrimitiveType.Triangles, (uint)_uploadedScene.Indices.Count, DrawElementsType.UnsignedInt, null);
        }
        else
        {
            foreach (var batch in _uploadedScene.DrawBatches)
            {
                var texture = GetTexture(batch.ColorTexture);
                if (texture != 0)
                {
                    _gl.ActiveTexture(TextureUnit.Texture0);
                    _gl.BindTexture(TextureTarget.Texture2D, texture);
                    _gl.Uniform1(_useTextureLocation, 1);
                }
                else
                {
                    _gl.BindTexture(TextureTarget.Texture2D, 0);
                    _gl.Uniform1(_useTextureLocation, 0);
                }

                _gl.DrawElements(
                    PrimitiveType.Triangles,
                    (uint)batch.IndexCount,
                    DrawElementsType.UnsignedInt,
                    (void*)(batch.StartIndex * sizeof(uint)));
            }
        }

        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _gl.BindVertexArray(0);
        _gl.UseProgram(0);

        CompletePendingCapture();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        var point = e.GetCurrentPoint(this);
        if (e.ClickCount == 2)
        {
            ResetCamera();
            return;
        }

        if (point.Properties.IsMiddleButtonPressed ||
            (point.Properties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Control)))
        {
            _isPanning = true;
            _lastPointerPosition = e.GetPosition(this);
            e.Pointer.Capture(this);
            return;
        }

        if (point.Properties.IsRightButtonPressed)
        {
            _isZooming = true;
            _lastPointerPosition = e.GetPosition(this);
            e.Pointer.Capture(this);
            return;
        }

        if (point.Properties.IsLeftButtonPressed)
        {
            _isRotating = true;
            _lastPointerPosition = e.GetPosition(this);
            e.Pointer.Capture(this);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isRotating = false;
        _isPanning = false;
        _isZooming = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isRotating && !_isPanning && !_isZooming)
        {
            return;
        }

        var position = e.GetPosition(this);
        var delta = position - _lastPointerPosition;
        _lastPointerPosition = position;

        if (_isPanning)
        {
            PanCamera(delta);
        }
        else if (_isZooming)
        {
            _distance = Math.Clamp(_distance * (1.0f + (float)delta.Y * 0.01f), 1.0f, 5000.0f);
        }
        else
        {
            _yaw += (float)delta.X * 0.01f;
            _pitch = Math.Clamp(_pitch + (float)delta.Y * 0.01f, -1.45f, 1.45f);
        }

        RequestNextFrameRendering();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _distance = Math.Clamp(_distance * (1.0f - (float)e.Delta.Y * 0.1f), 1.0f, 5000.0f);
        RequestNextFrameRendering();
    }

    private unsafe void UploadScene()
    {
        if (_gl is null)
        {
            return;
        }

        _sceneDirty = false;
        _uploadedScene = Scene;
        DeleteTextures();

        _gl.BindVertexArray(_vertexArray);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _indexBuffer);

        if (_uploadedScene is null || _uploadedScene.IsEmpty)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, 0, null, BufferUsageARB.StaticDraw);
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, 0, null, BufferUsageARB.StaticDraw);
            _gl.BindVertexArray(0);
            return;
        }

        var vertices = _uploadedScene.Vertices.ToArray();
        var indices = _uploadedScene.Indices.ToArray();

        fixed (PreviewVertex* vertexPtr = vertices)
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(vertices.Length * sizeof(PreviewVertex)),
                vertexPtr,
                BufferUsageARB.StaticDraw);
        }

        fixed (uint* indexPtr = indices)
        {
            _gl.BufferData(
                BufferTargetARB.ElementArrayBuffer,
                (nuint)(indices.Length * sizeof(uint)),
                indexPtr,
                BufferUsageARB.StaticDraw);
        }

        var stride = (uint)sizeof(PreviewVertex);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)Marshal.OffsetOf<PreviewVertex>(nameof(PreviewVertex.Normal)));
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, (void*)Marshal.OffsetOf<PreviewVertex>(nameof(PreviewVertex.Color)));
        _gl.EnableVertexAttribArray(3);
        _gl.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, stride, (void*)Marshal.OffsetOf<PreviewVertex>(nameof(PreviewVertex.TexCoord)));
        _gl.BindVertexArray(0);
    }

    private unsafe uint GetTexture(PreviewTextureData? textureData)
    {
        if (_gl is null || textureData is null)
        {
            return 0;
        }

        if (_textures.TryGetValue(textureData, out var existing))
        {
            return existing;
        }

        var texture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, texture);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

        fixed (byte* pixels = textureData.RgbaPixels)
        {
            _gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                InternalFormat.Rgba,
                (uint)textureData.Width,
                (uint)textureData.Height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                pixels);
        }

        _textures[textureData] = texture;
        return texture;
    }

    private void DeleteTextures()
    {
        if (_gl is null)
        {
            _textures.Clear();
            return;
        }

        foreach (var texture in _textures.Values)
        {
            _gl.DeleteTexture(texture);
        }

        _textures.Clear();
    }

    private void UpdateCameraFromScene(PreviewSceneData? scene)
    {
        if (scene is null || scene.IsEmpty)
        {
            _distance = 40.0f;
            _targetOffset = Vector3.Zero;
            return;
        }

        var extent = scene.Max - scene.Min;
        var radius = MathF.Max(MathF.Max(extent.X, extent.Y), extent.Z) * 0.75f;
        _distance = Math.Clamp(radius * 2.8f + 5.0f, 10.0f, 5000.0f);
        _targetOffset = Vector3.Zero;
    }

    private void ResetCamera()
    {
        _yaw = -0.8f;
        _pitch = 0.35f;
        UpdateCameraFromScene(Scene);
        RequestNextFrameRendering();
    }

    private void PanCamera(Point delta)
    {
        var right = Vector3.Normalize(new Vector3(MathF.Cos(_yaw), 0.0f, -MathF.Sin(_yaw)));
        var up = Vector3.UnitY;
        var scale = MathF.Max(_distance, 1.0f) * 0.0025f;
        _targetOffset += right * (float)(-delta.X * scale) + up * (float)(delta.Y * scale);
    }

    private static uint CreateProgram(GL gl)
    {
        const string vertexShaderSource = """
            #version 330 core
            layout (location = 0) in vec3 aPosition;
            layout (location = 1) in vec3 aNormal;
            layout (location = 2) in vec4 aColor;
            layout (location = 3) in vec2 aTexCoord;

            uniform mat4 uMvp;

            out vec3 vNormal;
            out vec4 vColor;
            out vec2 vTexCoord;

            void main()
            {
                gl_Position = uMvp * vec4(aPosition, 1.0);
                vNormal = aNormal;
                vColor = aColor;
                vTexCoord = aTexCoord;
            }
            """;

        const string fragmentShaderSource = """
            #version 330 core
            in vec3 vNormal;
            in vec4 vColor;
            in vec2 vTexCoord;

            uniform sampler2D uColorTexture;
            uniform int uUseTexture;

            out vec4 FragColor;

            void main()
            {
                vec3 lightDir = normalize(vec3(0.3, 0.8, 0.6));
                float lambert = max(dot(normalize(vNormal), lightDir), 0.15);
                vec4 baseColor = uUseTexture == 1 ? texture(uColorTexture, vTexCoord) : vColor;
                FragColor = vec4(baseColor.rgb * lambert, baseColor.a);
            }
            """;

        var vertexShader = CompileShader(gl, ShaderType.VertexShader, vertexShaderSource);
        var fragmentShader = CompileShader(gl, ShaderType.FragmentShader, fragmentShaderSource);
        var program = gl.CreateProgram();
        gl.AttachShader(program, vertexShader);
        gl.AttachShader(program, fragmentShader);
        gl.LinkProgram(program);
        gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var linked);
        if (linked == 0)
        {
            var log = gl.GetProgramInfoLog(program);
            gl.DeleteShader(vertexShader);
            gl.DeleteShader(fragmentShader);
            gl.DeleteProgram(program);
            throw new InvalidOperationException($"OpenGL 链接失败: {log}");
        }

        gl.DetachShader(program, vertexShader);
        gl.DetachShader(program, fragmentShader);
        gl.DeleteShader(vertexShader);
        gl.DeleteShader(fragmentShader);
        return program;
    }

    private unsafe void CompletePendingCapture()
    {
        if (_pendingCapture is null)
        {
            return;
        }

        var capture = CaptureCurrentFrame();
        _pendingCapture.SetResult(capture);
        _pendingCapture = null;
    }

    private unsafe PreviewFrameCapture? CaptureCurrentFrame()
    {
        if (_gl is null)
        {
            return null;
        }

        var width = Math.Max(1, (int)Bounds.Width);
        var height = Math.Max(1, (int)Bounds.Height);
        var bottomUp = new byte[width * height * 4];
        fixed (byte* pixels = bottomUp)
        {
            _gl.PixelStore(PixelStoreParameter.PackAlignment, 1);
            _gl.ReadPixels(0, 0, (uint)width, (uint)height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
        }

        var topDown = new byte[bottomUp.Length];
        var stride = width * 4;
        for (var y = 0; y < height; y++)
        {
            System.Buffer.BlockCopy(bottomUp, (height - 1 - y) * stride, topDown, y * stride, stride);
        }

        return new PreviewFrameCapture(width, height, topDown);
    }

    private static uint CompileShader(GL gl, ShaderType type, string source)
    {
        var shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);
        gl.GetShader(shader, ShaderParameterName.CompileStatus, out var compiled);
        if (compiled == 0)
        {
            var log = gl.GetShaderInfoLog(shader);
            gl.DeleteShader(shader);
            throw new InvalidOperationException($"OpenGL 着色器编译失败: {log}");
        }

        return shader;
    }
}

public sealed record PreviewFrameCapture(int Width, int Height, byte[] RgbaPixels);
