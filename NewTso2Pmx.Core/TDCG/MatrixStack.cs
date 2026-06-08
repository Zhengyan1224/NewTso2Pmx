using System.Collections.Generic;
using Microsoft.DirectX;

namespace TDCG;

public sealed class MatrixStack
{
    private readonly Stack<Matrix> _stack = new();

    public MatrixStack()
    {
        _stack.Push(Matrix.Identity);
    }

    public Matrix Top => _stack.Peek();

    public void LoadMatrix(Matrix matrix)
    {
        _stack.Pop();
        _stack.Push(matrix);
    }

    public void Push()
    {
        _stack.Push(Top);
    }

    public void Pop()
    {
        if (_stack.Count > 1)
        {
            _stack.Pop();
        }
    }

    public void MultiplyMatrixLocal(Matrix matrix)
    {
        var top = _stack.Pop();
        // D3DX MatrixStack.MultMatrixLocal left-multiplies the given matrix.
        _stack.Push(matrix * top);
    }
}
