using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using StbImageSharp;

namespace SuperbloomGame
{
    public class Game : GameWindow
    {
        private int _vbo, _puddleVao, _puddleVbo, _groundVao, _groundVbo, _ebo, _program;
        private List<int> _vaoGroups = new List<int>();
        private List<int> _instanceVboGroups = new List<int>();
        private List<int> _textures = new List<int>();
        private List<string> _textureFiles = new List<string>();
        private List<List<(float x, float z, Vector3 color, float scale)>> _flowerGroups = new List<List<(float x, float z, Vector3 color, float scale)>>();
        private Matrix4 _view, _projection;
        private Vector3 _cameraPos = new Vector3(0, 1.7f, 0);
        private float _yaw = -90f, _pitch;
        private Vector3 _cameraFront = new Vector3(0, 0, -1);
        private Vector3 _cameraGo = new Vector3(0, 0, -1);
        private Vector3 _cameraUp = Vector3.UnitY;
        private float _lastX, _lastY;
        private bool _firstMouse = true;
        private Random _random = new Random();
        private List<(float x, float z)> _puddles = new List<(float, float)>();
        private int _groundVertexCount;
        private Vector3 _lightPos = new Vector3(0f, 5f, 0f);
        private Vector3 _lightColor = new Vector3(1f, 1f, 1f);
        private float _ambientStrength = 0.2f;
        private bool _isFullscreen = false;
        private double _lastRenderTime = 0;
        private bool _isJumping = false;
        private float _jumpVelocity = 0f;
        private float _jumpTime = 0f;
        private float _gravity = -9.8f;

        public Game() : base(GameWindowSettings.Default, new NativeWindowSettings { Size = (800, 600), Title = "Superbloom Wanderer", Profile = ContextProfile.Core })
        {
        }

        float GroundHeight(float xPos, float zPos)
        {
            return 2f * (float)Math.Sin(xPos / 5f) * (float)Math.Cos(zPos / 5f);
        }

        Vector3 GroundNormal(float xPos, float zPos)
        {
            float dx = 0.4f * (float)Math.Cos(xPos / 5f) * (float)Math.Cos(zPos / 5f);
            float dz = -0.4f * (float)Math.Sin(xPos / 5f) * (float)Math.Sin(zPos / 5f);
            Vector3 normal = new Vector3(-dx, 1f, -dz);
            return Vector3.Normalize(normal);
        }

        protected override void OnLoad()
        {
            GLFW.SwapInterval(1);
            GL.ClearColor(0.7f, 0.9f, 1.0f, 1.0f);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.StencilTest);
            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            CheckGLError("After enabling states");

            CursorState = CursorState.Grabbed;

            string vertexShaderSource;
            string fragmentShaderSource;
            try
            {
                if (!File.Exists("Shaders/vertex.glsl"))
                    throw new FileNotFoundException("Vertex shader file not found", "Shaders/vertex.glsl");
                if (!File.Exists("Shaders/fragment.glsl"))
                    throw new FileNotFoundException("Fragment shader file not found", "Shaders/fragment.glsl");

                vertexShaderSource = File.ReadAllText("Shaders/vertex.glsl", Encoding.UTF8);
                fragmentShaderSource = File.ReadAllText("Shaders/fragment.glsl", Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(vertexShaderSource))
                    throw new Exception("Vertex shader source is empty");
                if (string.IsNullOrWhiteSpace(fragmentShaderSource))
                    throw new Exception("Fragment shader source is empty");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error reading shader files: {e.Message}");
                Close();
                return;
            }

            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexShaderSource);
            GL.CompileShader(vertexShader);
            CheckShaderError(vertexShader, "Vertex Shader");
            CheckGLError("After compiling vertex shader");

            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentShaderSource);
            GL.CompileShader(fragmentShader);
            CheckShaderError(fragmentShader, "Fragment Shader");
            CheckGLError("After compiling fragment shader");

            _program = GL.CreateProgram();
            GL.AttachShader(_program, vertexShader);
            GL.AttachShader(_program, fragmentShader);
            GL.LinkProgram(_program);
            CheckProgramError(_program, "Program");
            CheckGLError("After linking program");

            GL.ValidateProgram(_program);
            GL.GetProgram(_program, GetProgramParameterName.ValidateStatus, out int validateStatus);
            if (validateStatus == 0)
            {
                string infoLog = GL.GetProgramInfoLog(_program);
                Console.WriteLine($"Program Validation Warning: {infoLog}");
            }

            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            string[] textureFiles = Directory.GetFiles("Textures", "*.png");
            if (textureFiles.Length == 0)
            {
                Console.WriteLine("No textures found in Textures folder");
                Close();
                return;
            }

            int maxTextures = Math.Min(textureFiles.Length, 8);
            if (textureFiles.Length > 8)
                Console.WriteLine($"Warning: Only the first 8 textures will be loaded (found {textureFiles.Length})");

            for (int i = 0; i < maxTextures; i++)
            {
                string file = textureFiles[i];
                _textureFiles.Add(Path.GetFileName(file));
                int texture = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, texture);
                CheckGLError($"After binding texture {file}");
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                CheckGLError($"After setting texture parameters for {file}");

                try
                {
                    using (var stream = File.OpenRead(file))
                    {
                        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
                        if (image.Width == 0 || image.Height == 0)
                            throw new Exception("Invalid texture dimensions");
                        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);
                        CheckGLError($"After loading texture data for {file}");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error loading texture {file}: {e.Message}");
                    GL.DeleteTexture(texture);
                    continue;
                }
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
                CheckGLError($"After generating mipmap for {file}");
                _textures.Add(texture);
            }

            GL.BindTexture(TextureTarget.Texture2D, 0);
            if (_textures.Count == 0)
            {
                Console.WriteLine("No valid textures loaded");
                Close();
                return;
            }

            float[] vertices = {
                -0.5f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f,
                 0.5f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 1.0f, 1.0f,
                 0.5f, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                -0.5f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f,
                 0.5f, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                -0.5f, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f
            };

            _vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
            CheckGLError("After setting flower VBO");

            for (int i = 0; i < _textures.Count; i++)
            {
                var flowerGroup = new List<(float x, float z, Vector3 color, float scale)>();
                _flowerGroups.Add(flowerGroup);
                GenerateFlowersGroup(flowerGroup, 75, i, _textureFiles);

                int vao = GL.GenVertexArray();
                GL.BindVertexArray(vao);
                GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
                GL.EnableVertexAttribArray(0);
                GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float));
                GL.EnableVertexAttribArray(1);
                GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 6 * sizeof(float));
                GL.EnableVertexAttribArray(2);
                CheckGLError($"After setting vertex attributes for Group {i}");
                GL.BindVertexArray(0);
                _vaoGroups.Add(vao);

                int instanceVbo = SetupFlowerGroupInstancing(flowerGroup, vao);
                _instanceVboGroups.Add(instanceVbo);
            }

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

            float[] puddleVertices = {
                -0.5f, 0.0f, -0.5f, 0.0f, 1.0f, 0.0f,
                 0.5f, 0.0f, -0.5f, 0.0f, 1.0f, 0.0f,
                 0.5f, 0.0f,  0.5f, 0.0f, 1.0f, 0.0f,
                -0.5f, 0.0f, -0.5f, 0.0f, 1.0f, 0.0f,
                 0.5f, 0.0f,  0.5f, 0.0f, 1.0f, 0.0f,
                -0.5f, 0.0f,  0.5f, 0.0f, 1.0f, 0.0f
            };
            _puddleVao = GL.GenVertexArray();
            _puddleVbo = GL.GenBuffer();
            GL.BindVertexArray(_puddleVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _puddleVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, puddleVertices.Length * sizeof(float), puddleVertices, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
            CheckGLError("After setting vertex attributes for puddles");
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

            int gridSize = 50;
            float scale = 50f / gridSize;
            List<float> groundVertices = new List<float>();
            for (int z = 0; z < gridSize; z++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    float xPos = (x - gridSize / 2) * scale;
                    float zPos = (z - gridSize / 2) * scale;
                    float yPos = GroundHeight(xPos, zPos);
                    Vector3 normal = GroundNormal(xPos, zPos);
                    groundVertices.Add(xPos);
                    groundVertices.Add(yPos);
                    groundVertices.Add(zPos);
                    groundVertices.Add(normal.X);
                    groundVertices.Add(normal.Y);
                    groundVertices.Add(normal.Z);
                }
            }

            List<int> indices = new List<int>();
            for (int z = 0; z < gridSize - 1; z++)
            {
                for (int x = 0; x < gridSize - 1; x++)
                {
                    int topLeft = z * gridSize + x;
                    int topRight = topLeft + 1;
                    int bottomLeft = (z + 1) * gridSize + x;
                    int bottomRight = bottomLeft + 1;
                    indices.Add(topLeft);
                    indices.Add(bottomLeft);
                    indices.Add(topRight);
                    indices.Add(topRight);
                    indices.Add(bottomLeft);
                    indices.Add(bottomRight);
                }
            }

            _groundVao = GL.GenVertexArray();
            _groundVbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();
            GL.BindVertexArray(_groundVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _groundVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, groundVertices.Count * sizeof(float), groundVertices.ToArray(), BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(int), indices.ToArray(), BufferUsageHint.StaticDraw);
            CheckGLError("After setting ground VAO");
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            _groundVertexCount = indices.Count;

            _projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(60f), Size.X / (float)Size.Y, 0.1f, 100f);

            for (int i = 0; i < 5; i++)
            {
                float x = (float)(_random.NextDouble() * 30 - 15);
                float z = (float)(_random.NextDouble() * 30 - 15);
                _puddles.Add((x, z));
            }
        }

        private void GenerateFlowersGroup(List<(float x, float z, Vector3 color, float scale)> flowers, int count, int textureIndex, List<string> textureFiles)
        {
            HashSet<(float x, float z)> occupiedPositions = new HashSet<(float, float)>();
            const float minDistance = 0.5f;
            int flowerCount = 0;

            foreach (var group in _flowerGroups)
            {
                foreach (var flower in group)
                {
                    occupiedPositions.Add((flower.x, flower.z));
                }
            }

            while (flowerCount < count)
            {
                float x = (float)(_random.NextDouble() * 40 - 20);
                float z = (float)(_random.NextDouble() * 40 - 20);

                bool isUnique = true;
                foreach (var pos in occupiedPositions)
                {
                    float distance = (float)Math.Sqrt((x - pos.x) * (x - pos.x) + (z - pos.z) * (z - pos.z));
                    if (distance < minDistance)
                    {
                        isUnique = false;
                        break;
                    }
                }

                if (isUnique)
                {
                    occupiedPositions.Add((x, z));
                    Vector3 color = new Vector3(1f, 1f, 1f);
                    float flowerScale = (float)(0.3 + _random.NextDouble() * 0.2);
                    flowers.Add((x, z, color, flowerScale));
                    flowerCount++;
                }
            }
        }

        private int SetupFlowerGroupInstancing(List<(float x, float z, Vector3 color, float scale)> flowers, int vao)
        {
            if (flowers.Count == 0) return 0;

            float[] instanceData = new float[flowers.Count * 7];
            for (int i = 0; i < flowers.Count; i++)
            {
                var flower = flowers[i];
                instanceData[i * 7 + 0] = flower.x;
                instanceData[i * 7 + 1] = flower.z;
                instanceData[i * 7 + 2] = flower.color.X;
                instanceData[i * 7 + 3] = flower.color.Y;
                instanceData[i * 7 + 4] = flower.color.Z;
                instanceData[i * 7 + 5] = flower.scale;
                instanceData[i * 7 + 6] = flower.scale;
            }

            int instanceVbo = GL.GenBuffer();
            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, instanceVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, instanceData.Length * sizeof(float), instanceData, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, 7 * sizeof(float), 0);
            GL.EnableVertexAttribArray(3);
            GL.VertexAttribPointer(4, 3, VertexAttribPointerType.Float, false, 7 * sizeof(float), 2 * sizeof(float));
            GL.EnableVertexAttribArray(4);
            GL.VertexAttribPointer(5, 2, VertexAttribPointerType.Float, false, 7 * sizeof(float), 5 * sizeof(float));
            GL.EnableVertexAttribArray(5);
            GL.VertexAttribDivisor(3, 1);
            GL.VertexAttribDivisor(4, 1);
            GL.VertexAttribDivisor(5, 1);
            CheckGLError($"After setting instance attributes for VAO {vao}");
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            return instanceVbo;
        }

        private void CheckGLError(string stage)
        {
            int error = (int)GL.GetError();
            if (error != 0)
                Console.WriteLine($"OpenGL Error at {stage}: {error}");
        }

        private void CheckShaderError(int shader, string name)
        {
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(shader);
                Console.WriteLine($"{name} Error: {infoLog}");
            }
        }

        private void CheckProgramError(int program, string name)
        {
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetProgramInfoLog(program);
                Console.WriteLine($"{name} Error: {infoLog}");
            }
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            double currentTime = GLFW.GetTime();
            if (currentTime < _lastRenderTime + 1.0 / 60.0)
                return;
            _lastRenderTime = currentTime;

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
            CheckGLError("After clear");

            GL.UseProgram(_program);
            CheckGLError("After use program");

            _view = Matrix4.LookAt(_cameraPos, _cameraPos + _cameraFront, _cameraUp);
            GL.UniformMatrix4(GL.GetUniformLocation(_program, "view"), false, ref _view);
            GL.UniformMatrix4(GL.GetUniformLocation(_program, "projection"), false, ref _projection);
            GL.Uniform3(GL.GetUniformLocation(_program, "lightPos"), _lightPos);
            GL.Uniform3(GL.GetUniformLocation(_program, "lightColor"), _lightColor);
            GL.Uniform1(GL.GetUniformLocation(_program, "ambientStrength"), _ambientStrength);
            GL.Uniform3(GL.GetUniformLocation(_program, "cameraPos"), _cameraPos);
            CheckGLError("After setting uniforms");

            DrawGround();
            DrawFlowers();
            DrawPuddles();

            GL.Flush();
            CheckGLError("After flush");
            SwapBuffers();
        }

        private void DrawGround()
        {
            GL.BindVertexArray(_groundVao);
            Matrix4 model = Matrix4.Identity;
            GL.UniformMatrix4(GL.GetUniformLocation(_program, "model"), false, ref model);
            GL.Uniform3(GL.GetUniformLocation(_program, "objectColor"), 0.2f, 0.5f, 0.2f);
            GL.Uniform1(GL.GetUniformLocation(_program, "useTexture"), 0);
            GL.DrawElements(PrimitiveType.Triangles, _groundVertexCount, DrawElementsType.UnsignedInt, 0);
            CheckGLError("After drawing ground");
            GL.BindVertexArray(0);
        }

        private void DrawFlowers()
        {
            GL.Uniform1(GL.GetUniformLocation(_program, "useTexture"), 1);
            Matrix4 model = Matrix4.Identity;
            GL.UniformMatrix4(GL.GetUniformLocation(_program, "model"), false, ref model);

            for (int i = 0; i < _flowerGroups.Count; i++)
            {
                if (_flowerGroups[i].Count > 0 && i < _textures.Count && _instanceVboGroups[i] != 0 && _vaoGroups[i] != 0)
                {
                    GL.ActiveTexture(TextureUnit.Texture0);
                    GL.BindTexture(TextureTarget.Texture2D, _textures[i]);
                    GL.Uniform1(GL.GetUniformLocation(_program, "texture1"), 0);
                    GL.BindVertexArray(_vaoGroups[i]);
                    GL.DrawArraysInstanced(PrimitiveType.Triangles, 0, 6, _flowerGroups[i].Count);
                    CheckGLError($"After drawing flowers group {i}");
                    GL.BindVertexArray(0);
                }
            }
        }

        private void DrawPuddles()
        {
            GL.BindVertexArray(_puddleVao);
            GL.DisableVertexAttribArray(2);
            GL.DisableVertexAttribArray(3);
            GL.DisableVertexAttribArray(4);
            GL.DisableVertexAttribArray(5);
            GL.Uniform1(GL.GetUniformLocation(_program, "useTexture"), 0);

            foreach (var puddle in _puddles)
            {
                float y = GroundHeight(puddle.x, puddle.z) + 0.03f;
                Vector3 normal = GroundNormal(puddle.x, puddle.z);
                Vector3 up = new Vector3(0f, 1f, 0f);
                Vector3 axis = Vector3.Cross(up, normal);
                float angle = (float)Math.Acos(Vector3.Dot(up, normal) / (up.Length * normal.Length));
                Quaternion rotation = axis.Length > 0 ? Quaternion.FromAxisAngle(axis.Normalized(), angle) : Quaternion.Identity;
                Matrix4 rotationMatrix = Matrix4.CreateFromQuaternion(rotation);
                Matrix4 model = Matrix4.CreateScale(2f, 1f, 2f) * rotationMatrix * Matrix4.CreateTranslation(puddle.x, y, puddle.z);
                GL.UniformMatrix4(GL.GetUniformLocation(_program, "model"), false, ref model);
                GL.Uniform3(GL.GetUniformLocation(_program, "objectColor"), 0.5f, 0.6f, 1.0f);
                GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
                CheckGLError("After drawing puddle");
            }

            GL.EnableVertexAttribArray(2);
            GL.EnableVertexAttribArray(3);
            GL.EnableVertexAttribArray(4);
            GL.EnableVertexAttribArray(5);
            GL.Enable(EnableCap.StencilTest);
            GL.BindVertexArray(0);
            CheckGLError("After drawing puddles");
        }

        bool run = false;
        bool sneak = false;

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            if (!IsFocused) return;

            var keyboard = KeyboardState;
            float baseSpeed = 5.0f * (float)args.Time;
            float speed = baseSpeed;

            if (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift))
            {
                if (!sneak && !run)
                {
                    speed *= 0.5f;
                    sneak = true;
                }
                else if (!run)
                {
                    speed *= 2f;
                    sneak = true;
                }
            }
            if (keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl))
            {
                if (!sneak && !run)
                {
                    speed *= 2f;
                    sneak = true;
                }
                else if (!run)
                {
                    speed *= 0.5f;
                    sneak = true;
                }
            }

            if (keyboard.IsKeyPressed(Keys.Escape))
            {
                CursorState = CursorState.Normal;
                Close();
                return;
            }

            if (keyboard.IsKeyPressed(Keys.F11))
            {
                _isFullscreen = !_isFullscreen;
                WindowState = _isFullscreen ? WindowState.Fullscreen : WindowState.Normal;
            }

            if (keyboard.IsKeyPressed(Keys.Space) && !_isJumping)
            {
                _isJumping = true;
                _jumpVelocity = 3.5f;
                _jumpTime = 0f;
            }

            Vector3 newPos = _cameraPos;
            if (keyboard.IsKeyDown(Keys.W))
            {
                newPos += speed * _cameraGo;
                newPos[1] = GroundHeight(newPos[0], newPos[2]) + 1.7f;
            }
            if (keyboard.IsKeyDown(Keys.S))
            {
                newPos -= speed * _cameraGo;
                newPos[1] = GroundHeight(newPos[0], newPos[2]) + 1.7f;
            }
            if (keyboard.IsKeyDown(Keys.A))
            {
                newPos -= speed * Vector3.Normalize(Vector3.Cross(_cameraGo, _cameraUp));
                newPos[1] = GroundHeight(newPos[0], newPos[2]) + 1.7f;
            }
            if (keyboard.IsKeyDown(Keys.D))
            {
                newPos += speed * Vector3.Normalize(Vector3.Cross(_cameraGo, _cameraUp));
                newPos[1] = GroundHeight(newPos[0], newPos[2]) + 1.7f;
            }

            if (_isJumping)
            {
                _jumpTime += (float)args.Time;
                float jumpHeight = _jumpVelocity * _jumpTime + 0.5f * _gravity * _jumpTime * _jumpTime;
                newPos.Y = GroundHeight(newPos.X, newPos.Z) + jumpHeight + 1.7f;
                if (newPos.Y <= GroundHeight(newPos.X, newPos.Z) + 1.7f)
                {
                    newPos.Y = GroundHeight(newPos.X, newPos.Z) + 1.7f;
                    _isJumping = false;
                    _jumpVelocity = 0f;
                    _jumpTime = 0f;
                }
            }
            else
            {
                newPos.Y = Math.Max(GroundHeight(newPos.X, newPos.Z) + 0.5f, newPos.Y);
            }

            _cameraPos = newPos;
        }

        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            if (_firstMouse)
            {
                _lastX = e.X;
                _lastY = e.Y;
                _firstMouse = false;
            }

            float xoffset = e.X - _lastX;
            float yoffset = _lastY - e.Y;
            _lastX = e.X;
            _lastY = e.Y;

            float sensitivity = 0.1f;
            xoffset *= sensitivity;
            yoffset *= sensitivity;

            _yaw += xoffset;
            _pitch = MathHelper.Clamp(_pitch + yoffset, -89f, 89f);

            Vector3 front;
            front.X = (float)Math.Cos(MathHelper.DegreesToRadians(_yaw)) * (float)Math.Cos(MathHelper.DegreesToRadians(_pitch));
            front.Y = (float)Math.Sin(MathHelper.DegreesToRadians(_pitch));
            front.Z = (float)Math.Sin(MathHelper.DegreesToRadians(_yaw)) * (float)Math.Cos(MathHelper.DegreesToRadians(_pitch));
            _cameraFront = Vector3.Normalize(front);
            front.Y = 0;
            _cameraGo = Vector3.Normalize(front);
        }

        protected override void OnMouseEnter()
        {
            if (WindowState != WindowState.Minimized)
                CursorState = CursorState.Grabbed;
        }

        protected override void OnMouseLeave()
        {
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            GL.Viewport(0, 0, e.Width, e.Height);
            _projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(60f), e.Width / (float)e.Height, 0.1f, 100f);
        }

        protected override void OnUnload()
        {
            GL.DeleteBuffer(_vbo);
            GL.DeleteBuffer(_puddleVbo);
            GL.DeleteBuffer(_groundVbo);
            GL.DeleteBuffer(_ebo);
            foreach (var vbo in _instanceVboGroups)
                if (vbo != 0)
                    GL.DeleteBuffer(vbo);
            foreach (var vao in _vaoGroups)
                if (vao != 0)
                    GL.DeleteVertexArray(vao);
            GL.DeleteVertexArray(_puddleVao);
            GL.DeleteVertexArray(_groundVao);
            foreach (var texture in _textures)
                GL.DeleteTexture(texture);
            GL.DeleteProgram(_program);
        }

        public static void Main()
        {
            using (var game = new Game())
            {
                game.Run();
            }
        }
    }
}