using System.Numerics;
using Raylib_cs;

namespace Game;

class Program
{
    private const int SCREEN_WIDTH = 2560;
    private const int SCREEN_HEIGHT = 1080;
    private static Shader _lightingShader;
    private static Texture2D _spriteTexture;
    private static RenderTexture2D _normalMapTarget;

    [STAThread]
    public static void Main()
    {
        Raylib.InitWindow(SCREEN_WIDTH, SCREEN_HEIGHT, "Normal Map Generator");
        Raylib.SetTargetFPS(60);

        _lightingShader = Raylib.LoadShader("lighting.vs", "lighting.fs");
        
        // 加载精灵图
        _spriteTexture = Raylib.LoadTexture(@"C:\Users\16018\Desktop\1.jpg"); // 你的精灵图文件
        _normalMapTarget = Raylib.LoadRenderTexture(_spriteTexture.Width, _spriteTexture.Height);

        // 设置高度缩放参数
        int heightScaleLoc = Raylib.GetShaderLocation(_lightingShader, "heightScale");
        Raylib.SetShaderValue(_lightingShader, heightScaleLoc, 30f, ShaderUniformDataType.Float);

        while (!Raylib.WindowShouldClose())
        {
            // 生成法线贴图
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(128, 128, 255, 255)); // 默认法线颜色
            Raylib.BeginShaderMode(_lightingShader);
            Raylib.DrawTexture(_spriteTexture, 0, 0, Color.White);
            Raylib.EndShaderMode();
            Raylib.EndDrawing();

            // 显示结果
            // Raylib.BeginDrawing();


            // Raylib.ClearBackground(Color.Black);
            
            // // 原始精灵图
            // Raylib.DrawTextureEx(_spriteTexture, new Vector2(50, 50), 0, 4.0f, Color.White);
            
            // // 生成的法线贴图
            // Raylib.DrawTextureRec(_normalMapTarget.Texture, 
            //     new Rectangle(0, 0, _normalMapTarget.Texture.Width, -_normalMapTarget.Texture.Height),
            //     new Vector2(400, 50), Color.White);
            // Raylib.DrawTextureEx(_normalMapTarget.Texture, new Vector2(400, 300), 0, 4.0f, Color.White);
            
            // Raylib.DrawText("Original Sprite", 50, 20, 20, Color.White);
            // Raylib.DrawText("Generated Normal Map", 400, 20, 20, Color.White);
            // Raylib.DrawText("4x Scaled", 400, 270, 20, Color.White);
            
            // Raylib.EndDrawing();
        }

        Raylib.UnloadTexture(_spriteTexture);
        Raylib.UnloadRenderTexture(_normalMapTarget);
        Raylib.UnloadShader(_lightingShader);
        Raylib.CloseWindow();
    }
}