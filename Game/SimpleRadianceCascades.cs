using System.Numerics;
using Raylib_cs;

namespace Game;

// 简化版本的Radiance Cascades演示
public class SimpleRadianceCascades
{
    private const int SCREEN_WIDTH = 1280;
    private const int SCREEN_HEIGHT = 720;
    
    public static void Run()
    {
        // 初始化
        Raylib.InitWindow(SCREEN_WIDTH, SCREEN_HEIGHT, "Simple Radiance Cascades 2D");
        Raylib.SetTargetFPS(60);
        
        // 简单的场景设置
        Vector2 lightPos = new Vector2(SCREEN_WIDTH / 2, SCREEN_HEIGHT / 2);
        Color lightColor = Color.Yellow;
        float lightIntensity = 1.0f;
        
        // 简单的探针网格
        int probeCountX = 16;
        int probeCountY = 9;
        float probeSpacing = SCREEN_WIDTH / (float)probeCountX;
        
        // 存储每个探针的光照信息
        Vector3[,] probeRadiance = new Vector3[probeCountX, probeCountY];
        
        Console.WriteLine("简化版Radiance Cascades启动成功！");
        Console.WriteLine("控制：鼠标移动光源，ESC退出");
        
        // 主循环
        while (!Raylib.WindowShouldClose())
        {
            // 更新光源位置
            if (Raylib.IsMouseButtonDown(MouseButton.Left))
            {
                lightPos = Raylib.GetMousePosition();
            }
            
            // 调整光照强度
            if (Raylib.IsKeyDown(KeyboardKey.Up))
                lightIntensity = Math.Min(3.0f, lightIntensity + 0.02f);
            if (Raylib.IsKeyDown(KeyboardKey.Down))
                lightIntensity = Math.Max(0.1f, lightIntensity - 0.02f);
            
            // 简化的光照计算
            UpdateSimpleLighting(probeRadiance, lightPos, lightColor, lightIntensity, probeSpacing);
            
            // 渲染
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            
            // 绘制光照效果
            DrawLighting(probeRadiance, probeSpacing);
            
            // 绘制场景几何体
            DrawSimpleScene();
            
            // 绘制光源
            Raylib.DrawCircleV(lightPos, 10, lightColor);
            
            // 绘制信息
            Raylib.DrawText("Simple Radiance Cascades Demo", 10, 10, 20, Color.White);
            Raylib.DrawText($"Light Intensity: {lightIntensity:F2}", 10, 40, 16, Color.White);
            Raylib.DrawText("Left Click: Move Light | Up/Down: Intensity", 10, SCREEN_HEIGHT - 30, 14, Color.Green);
            
            Raylib.EndDrawing();
        }
        
        // 清理
        Raylib.CloseWindow();
        Console.WriteLine("程序正常退出");
    }
    
    private static void UpdateSimpleLighting(Vector3[,] probeRadiance, Vector2 lightPos, Color lightColor, float intensity, float spacing)
    {
        int probeCountX = probeRadiance.GetLength(0);
        int probeCountY = probeRadiance.GetLength(1);
        
        for (int y = 0; y < probeCountY; y++)
        {
            for (int x = 0; x < probeCountX; x++)
            {
                Vector2 probePos = new Vector2(x * spacing + spacing * 0.5f, y * spacing + spacing * 0.5f);
                
                // 计算到光源的距离
                float distToLight = Vector2.Distance(probePos, lightPos);
                
                // 简单的衰减计算
                float attenuation = 1.0f / (1.0f + distToLight * 0.001f);
                
                // 检查是否被遮挡（简化版）
                bool occluded = IsOccluded(probePos, lightPos);
                
                if (!occluded)
                {
                    probeRadiance[x, y] = new Vector3(
                        lightColor.R / 255.0f * attenuation * intensity,
                        lightColor.G / 255.0f * attenuation * intensity,
                        lightColor.B / 255.0f * attenuation * intensity
                    );
                }
                else
                {
                    probeRadiance[x, y] = Vector3.Zero;
                }
            }
        }
    }
    
    private static bool IsOccluded(Vector2 from, Vector2 to)
    {
        // 简化的遮挡检测
        Vector2 direction = Vector2.Normalize(to - from);
        float distance = Vector2.Distance(from, to);
        
        // 检查是否与场景几何体相交
        for (float t = 10; t < distance; t += 5)
        {
            Vector2 testPos = from + direction * t;
            if (IsInsideGeometry(testPos))
                return true;
        }
        
        return false;
    }
    
    private static bool IsInsideGeometry(Vector2 pos)
    {
        // 简单的几何体检测
        if (Vector2.Distance(pos, new Vector2(300, 200)) < 50) return true;
        if (Vector2.Distance(pos, new Vector2(800, 400)) < 80) return true;
        if (pos.X > 540 && pos.X < 660 && pos.Y > 100 && pos.Y < 300) return true;
        
        return false;
    }
    
    private static void DrawLighting(Vector3[,] probeRadiance, float spacing)
    {
        int probeCountX = probeRadiance.GetLength(0);
        int probeCountY = probeRadiance.GetLength(1);
        
        // 绘制光照效果
        for (int y = 0; y < probeCountY - 1; y++)
        {
            for (int x = 0; x < probeCountX - 1; x++)
            {
                Vector2 pos = new Vector2(x * spacing, y * spacing);
                
                // 获取四个角的光照值
                Vector3 tl = probeRadiance[x, y];
                Vector3 tr = probeRadiance[x + 1, y];
                Vector3 bl = probeRadiance[x, y + 1];
                Vector3 br = probeRadiance[x + 1, y + 1];
                
                // 简单的颜色混合
                Vector3 avgRadiance = (tl + tr + bl + br) / 4.0f;
                
                if (avgRadiance.Length() > 0.01f)
                {
                    Color lightingColor = new Color(
                        (int)Math.Min(255, avgRadiance.X * 255),
                        (int)Math.Min(255, avgRadiance.Y * 255),
                        (int)Math.Min(255, avgRadiance.Z * 255),
                        100
                    );
                    
                    Raylib.DrawRectangle((int)pos.X, (int)pos.Y, (int)spacing, (int)spacing, lightingColor);
                }
            }
        }
    }
    
    private static void DrawSimpleScene()
    {
        // 绘制简单的几何体
        Color geometryColor = new Color(120, 120, 120, 200);
        Raylib.DrawCircle(300, 200, 50, geometryColor);
        Raylib.DrawCircle(800, 400, 80, geometryColor);
        Raylib.DrawRectangle(540, 100, 120, 200, geometryColor);
    }
}
