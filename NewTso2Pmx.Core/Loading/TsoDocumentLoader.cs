using System.Diagnostics;
using Microsoft.DirectX;
using TDCG;
using TDCGUtils;

namespace NewTso2Pmx.Core.Loading;

public sealed class TsoDocumentLoader
{
    public LoadedDocument Load(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        if (Directory.Exists(sourcePath))
        {
            return LoadDirectory(sourcePath);
        }

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        return extension switch
        {
            ".tso" => LoadTso(sourcePath),
            ".png" => LoadPng(sourcePath),
            _ => throw new NotSupportedException($"不支持的输入类型: {sourcePath}")
        };
    }

    public LoadedDocument Load(IReadOnlyList<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var files = sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
        if (files.Length == 0)
        {
            throw new ArgumentException("No input files were provided.", nameof(sourcePaths));
        }

        if (files.Length == 1)
        {
            return Load(files[0]);
        }

        if (files.Any(path => !IsTsoPath(path)))
        {
            throw new NotSupportedException("Multiple input selection only supports .tso files.");
        }

        return LoadTsoFiles(files, files[0], DocumentKind.TsoFileSet);
    }

    public LoadedDocument Load(string sourcePath, int figureIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        if (Directory.Exists(sourcePath))
        {
            if (figureIndex != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(figureIndex));
            }

            return LoadDirectory(sourcePath);
        }

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        return extension switch
        {
            ".tso" when figureIndex == 0 => LoadTso(sourcePath),
            ".tso" => throw new ArgumentOutOfRangeException(nameof(figureIndex)),
            ".png" => LoadPng(sourcePath, figureIndex),
            _ => throw new NotSupportedException($"Unsupported input type: {sourcePath}")
        };
    }

    public LoadedDocument Rebuild(LoadedDocument document, IReadOnlyList<string> categories)
    {
        ArgumentNullException.ThrowIfNull(document);

        return CreateDocument(
            document.SourcePath,
            document.Kind,
            document.Figure,
            categories,
            document.FigureIndex,
            document.FigureCount,
            document.OwnsFigure);
    }

    public LoadedFigureSession LoadSession(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        if (Directory.Exists(sourcePath))
        {
            return CreateSingleFigureSession(LoadDirectory(sourcePath));
        }

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        return extension switch
        {
            ".tso" => CreateSingleFigureSession(LoadTso(sourcePath)),
            ".png" => LoadPngSession(sourcePath),
            _ => throw new NotSupportedException($"不支持的输入类型: {sourcePath}")
        };
    }

    public LoadedFigureSession LoadSession(IReadOnlyList<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        return CreateSingleFigureSession(Load(sourcePaths));
    }

    public LoadedDocument CreateDocument(LoadedFigureSession session, int figureIndex)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (figureIndex < 0 || figureIndex >= session.Figures.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(figureIndex));
        }

        var figure = session.Figures[figureIndex];
        return CreateDocument(
            session.SourcePath,
            session.Kind,
            figure.Figure,
            figure.Categories,
            figureIndex,
            session.Figures.Count,
            ownsFigure: false);
    }

    public TdcgPoseInfo LoadPosePng(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var payload = LoadPngPayload(sourcePath);
        if (!string.Equals(payload.Type, "POSE", StringComparison.Ordinal))
        {
            throw new InvalidDataException("PNG 不是 pose-only 姿势 PNG。");
        }

        if (payload.Tmo is null)
        {
            throw new InvalidDataException("姿势 PNG 不包含 FTMO 数据。");
        }

        return new TdcgPoseInfo(payload.Tmo, payload.LightDirection);
    }

    private LoadedDocument LoadTso(string sourcePath)
        => LoadTsoFiles([sourcePath], sourcePath, DocumentKind.TsoFile);

    private static LoadedFigureSession CreateSingleFigureSession(LoadedDocument document)
        => new(
            document.SourcePath,
            document.Kind,
            [new LoadedFigureInfo(document.Figure, document.Categories)]);

    private static LoadedFigureSession LoadPngSession(string sourcePath)
    {
        var payload = LoadPngPayload(sourcePath);
        if (payload.Figures.Count == 0)
        {
            throw new InvalidDataException("PNG 中没有找到 Figure 数据。");
        }

        var figures = new List<LoadedFigureInfo>(payload.Figures.Count);
        for (var index = 0; index < payload.Figures.Count; index++)
        {
            figures.Add(new LoadedFigureInfo(
                payload.Figures[index],
                GetPngCategories(sourcePath, payload.Figures, index)));
        }

        return new LoadedFigureSession(sourcePath, DocumentKind.SavePng, figures);
    }

    private static LoadedDocument LoadTsoFiles(
        IReadOnlyList<string> files,
        string sourcePath,
        DocumentKind kind)
    {
        var figure = new Figure();
        var categories = new List<string>(files.Count);

        foreach (var file in files)
        {
            var tso = new TSOFile();
            tso.Load(file);
            figure.AddTSO(tso);
            categories.Add(Path.GetFileNameWithoutExtension(file));
        }

        figure.UpdateNodeMapAndBoneMatrices();

        return CreateDocument(
            sourcePath,
            kind,
            figure,
            categories);
    }

    private LoadedDocument LoadDirectory(string sourcePath)
    {
        var files = Directory.EnumerateFiles(sourcePath, "*", SearchOption.TopDirectoryOnly)
            .Where(IsTsoPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            throw new FileNotFoundException("目录中没有找到 .TSO 文件。", sourcePath);
        }

        return LoadTsoFiles(files, sourcePath, DocumentKind.TsoDirectory);
    }

    private static bool IsTsoPath(string path)
        => Path.GetExtension(path).Equals(".tso", StringComparison.OrdinalIgnoreCase);

    private LoadedDocument LoadPng(string sourcePath)
    {
        var payload = LoadPngPayload(sourcePath);
        var figure = payload.Figures.FirstOrDefault()
                     ?? throw new InvalidDataException("PNG 中没有可转换的模型数据。");

        var categories = new PNGFileUtils().GetCategoryList(sourcePath);
        if (categories.Count == 0)
        {
            categories.Add("未分类");
        }

        return CreateDocument(
            sourcePath,
            DocumentKind.SavePng,
            figure,
            categories);
    }

    private LoadedDocument LoadPng(string sourcePath, int figureIndex)
    {
        var payload = LoadPngPayload(sourcePath);
        if (figureIndex < 0 || figureIndex >= payload.Figures.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(figureIndex));
        }

        var categories = GetPngCategories(sourcePath, payload.Figures, figureIndex);

        return CreateDocument(
            sourcePath,
            DocumentKind.SavePng,
            payload.Figures[figureIndex],
            categories,
            figureIndex,
            payload.Figures.Count);
    }

    private static List<string> GetPngCategories(
        string sourcePath,
        IReadOnlyList<Figure> figures,
        int figureIndex)
    {
        var flatCategories = new PNGFileUtils().GetCategoryList(sourcePath);
        var offset = figures.Take(figureIndex).Sum(figure => figure.TSOList.Count);
        var count = figures[figureIndex].TSOList.Count;
        var categories = flatCategories.Skip(offset).Take(count).ToList();

        while (categories.Count < count)
        {
            categories.Add($"Object {categories.Count + 1}");
        }

        return categories;
    }

    private static LoadedDocument CreateDocument(
        string sourcePath,
        DocumentKind kind,
        Figure figure,
        IReadOnlyList<string> categories,
        int figureIndex = 0,
        int figureCount = 1,
        bool ownsFigure = true)
    {
        var tsoInfos = new List<LoadedTsoInfo>(figure.TSOList.Count);
        var materials = new List<LoadedMaterialInfo>();
        var subMeshes = new List<FlatSubMeshInfo>();

        for (var tsoIndex = 0; tsoIndex < figure.TSOList.Count; tsoIndex++)
        {
            var tso = figure.TSOList[tsoIndex];
            var category = categories.Count > tsoIndex ? categories[tsoIndex] : $"对象 {tsoIndex + 1}";
            tsoInfos.Add(new LoadedTsoInfo(
                tsoIndex,
                category,
                category,
                tso));

            for (var materialIndex = 0; materialIndex < tso.sub_scripts.Length; materialIndex++)
            {
                var subScript = tso.sub_scripts[materialIndex];
                materials.Add(new LoadedMaterialInfo(
                    tsoIndex,
                    materialIndex,
                    category,
                    subScript.Name,
                    subScript.FileName,
                    subScript.shader));
            }
        }

        var flatIndex = 0;
        for (var tsoIndex = 0; tsoIndex < figure.TSOList.Count; tsoIndex++)
        {
            var tso = figure.TSOList[tsoIndex];
            var category = categories.Count > tsoIndex ? categories[tsoIndex] : $"对象 {tsoIndex + 1}";

            for (var scriptIndex = 0; scriptIndex < tso.sub_scripts.Length; scriptIndex++)
            {
                for (var meshIndex = 0; meshIndex < tso.meshes.Length; meshIndex++)
                {
                    var mesh = tso.meshes[meshIndex];
                    for (var meshSubIndex = 0; meshSubIndex < mesh.sub_meshes.Length; meshSubIndex++)
                    {
                        var subMesh = mesh.sub_meshes[meshSubIndex];
                        if (subMesh.spec != scriptIndex)
                        {
                            continue;
                        }

                        subMeshes.Add(new FlatSubMeshInfo(
                            flatIndex++,
                            tsoIndex,
                            scriptIndex,
                            meshIndex,
                            meshSubIndex,
                            category,
                            tso.sub_scripts[scriptIndex].Name,
                            mesh.Name,
                            subMesh.vertices.Length,
                            subMesh));
                    }
                }
            }
        }

        return new LoadedDocument(
            sourcePath,
            kind,
            figure,
            categories,
            tsoInfos,
            subMeshes,
            materials,
            figureIndex,
            figureCount,
            ownsFigure);
    }
    private static PngPayload LoadPngPayload(string sourcePath)
    {
        var payload = new PngPayload();
        var png = new PNGFile();
        Figure? currentFigure = null;

        png.Hsav += type =>
        {
            payload.Type = type;
            currentFigure = new Figure();
            payload.Figures.Add(currentFigure);
        };
        png.Pose += type => payload.Type = type;
        png.Scne += type => payload.Type = type;
        png.Lgta += (stream, length) =>
        {
            payload.LightDirection = ReadLightDirection(stream, length);
        };
        png.Ftmo += (stream, _) =>
        {
            payload.Tmo = new TMOFile();
            payload.Tmo.Load(stream);
        };
        png.Figu += (stream, length) =>
        {
            currentFigure = new Figure
            {
                LightDirection = payload.LightDirection,
                Tmo = payload.Tmo
            };

            payload.Figures.Add(currentFigure);

            var ratios = ReadSingleArray(stream, length);
            if (ratios.Count > 5)
            {
                currentFigure.slider_matrix.TallRatio = ratios[0];
                currentFigure.slider_matrix.ArmRatio = ratios[1];
                currentFigure.slider_matrix.LegRatio = ratios[2];
                currentFigure.slider_matrix.WaistRatio = ratios[3];
                currentFigure.slider_matrix.BustRatio = ratios[4];
                currentFigure.slider_matrix.EyeRatio = ratios[5];
            }
        };
        png.Ftso += (stream, _, _) =>
        {
            if (currentFigure is null)
            {
                return;
            }

            var tso = new TSOFile();
            tso.Load(stream);
            currentFigure.TSOList.Add(tso);
        };

        Debug.WriteLine("loading " + sourcePath);
        png.Load(sourcePath);

        if (payload.Type == "HSAV")
        {
            using var memoryStream = new MemoryStream();
            png.Save(memoryStream);
            memoryStream.Position = 0;
            var data = BmpSaveData.Read(memoryStream);
            var figure = payload.Figures.LastOrDefault();
            if (figure is not null)
            {
                figure.slider_matrix.TallRatio = data.Proportions[1];
                figure.slider_matrix.ArmRatio = data.Proportions[2];
                figure.slider_matrix.LegRatio = data.Proportions[3];
                figure.slider_matrix.WaistRatio = data.Proportions[4];
                figure.slider_matrix.BustRatio = data.Proportions[0];
                figure.slider_matrix.EyeRatio = data.Proportions[5];
            }
        }

        foreach (var figure in payload.Figures)
        {
            figure.UpdateNodeMapAndBoneMatrices();
        }

        return payload;
    }

    private static Vector3 ReadLightDirection(Stream stream, int length)
    {
        var factor = ReadSingleArray(stream, length);
        if (factor.Count < 16)
        {
            return Vector3.Empty;
        }

        Matrix m;
        m.M11 = factor[0];
        m.M12 = factor[1];
        m.M13 = factor[2];
        m.M14 = factor[3];
        m.M21 = factor[4];
        m.M22 = factor[5];
        m.M23 = factor[6];
        m.M24 = factor[7];
        m.M31 = factor[8];
        m.M32 = factor[9];
        m.M33 = factor[10];
        m.M34 = factor[11];
        m.M41 = factor[12];
        m.M42 = factor[13];
        m.M43 = factor[14];
        m.M44 = factor[15];

        return Vector3.TransformCoordinate(new Vector3(0.0f, 0.0f, -1.0f), m);
    }

    private static List<float> ReadSingleArray(Stream stream, int length)
    {
        var buffer = new byte[length];
        stream.ReadExactly(buffer, 0, length);

        var values = new List<float>(length / sizeof(float));
        for (var offset = 0; offset < length; offset += sizeof(float))
        {
            values.Add(BitConverter.ToSingle(buffer, offset));
        }

        return values;
    }

    private sealed class PngPayload
    {
        public string? Type { get; set; }

        public Vector3 LightDirection { get; set; } = Vector3.Empty;

        public TMOFile? Tmo { get; set; }

        public List<Figure> Figures { get; } = [];
    }
}
