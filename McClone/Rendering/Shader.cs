using OpenTK.Graphics.OpenGL4;
using System.IO;
using System;
using OpenTK.Mathematics; // Ensure this is included

namespace VoxelGame.Rendering
{

public class Shader : IDisposable
{
    public int Handle { get; private set; }
    private bool _disposedValue = false;

    public Shader(string vertexPath, string fragmentPath)
    {
        // Load shader source code
        string vertexShaderSource = File.ReadAllText(vertexPath);
        string fragmentShaderSource = File.ReadAllText(fragmentPath);

        // Compile shaders
        int vertexShader = CompileShader(vertexShaderSource, ShaderType.VertexShader);
        int fragmentShader = CompileShader(fragmentShaderSource, ShaderType.FragmentShader);

        // Create shader program
        Handle = GL.CreateProgram();
        GL.AttachShader(Handle, vertexShader);
        GL.AttachShader(Handle, fragmentShader);
        GL.LinkProgram(Handle);

        // Check for linking errors
        GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out int success);
        if (success == 0)
        {
            string infoLog = GL.GetProgramInfoLog(Handle);
            Console.WriteLine($"Shader Program Linking Error:\n{infoLog}");
            throw new Exception("Shader program linking failed.");
        }

        // Detach and delete shaders (they are linked now)
        GL.DetachShader(Handle, vertexShader);
        GL.DetachShader(Handle, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);
    }

    private int CompileShader(string source, ShaderType type)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);

        // Check for compile errors
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
        if (success == 0)
        {
            string infoLog = GL.GetShaderInfoLog(shader);
            Console.WriteLine($"Shader Compilation Error ({type}):\n{infoLog}");
            throw new Exception("Shader compilation failed.");
        }
        return shader;
    }

    public void Use()
    {
        GL.UseProgram(Handle);
    }

    public int GetAttribLocation(string attribName)
    {
        return GL.GetAttribLocation(Handle, attribName);
    }

    public int GetUniformLocation(string uniformName)
    {
        int location = GL.GetUniformLocation(Handle, uniformName);
        if (location == -1) // Uniform not found or unused (compiler optimized out)
        {
             Console.WriteLine($"Warning: Uniform '{uniformName}' not found in shader program {Handle}. It might be unused or misspelled.");
        }
        return location;
    }

    public void SetMatrix4(string name, Matrix4 data) // Use fully qualified name
    {
         Use(); // Ensure program is active
         int location = GetUniformLocation(name);
         if (location != -1)
         {
              GL.UniformMatrix4(location, false, ref data); // Use false for ColumnMajor (OpenGL default)
         }
    }

    public void SetVector3(string name, Vector3 data) // Use fully qualified name
    {
        Use();
        int location = GetUniformLocation(name);
        if (location != -1)
        {
            GL.Uniform3(location, data);
        }
    }

    public void SetFloat(string name, float data)
    {
        Use();
        int location = GetUniformLocation(name);
        if (location != -1)
        {
            GL.Uniform1(location, data);
        }
    }

    public void SetInt(string name, int data)
    {
        Use();
        int location = GetUniformLocation(name);
        if (location != -1)
        {
            GL.Uniform1(location, data);
        }
    }

    // Add a specific method for bool uniforms (sent as int 0 or 1)
    public void SetBool(string name, bool data)
    {
        SetInt(name, data ? 1 : 0);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            GL.DeleteProgram(Handle);
            _disposedValue = true;
        }
    }

    ~Shader()
    {
        Dispose(false); // Should be called by GC, but OpenGL context might be gone
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
}