using System.Numerics;
using Raylib_cs;

namespace Game;

// Radiance Cascades 2D 实现
public struct RadianceProbe
{
    public Vector2 Position;
    public Vector4[] Radiance; // 存储每个方向的辐射度 (RGB + 可见性)
    public int DirectionCount;

    public RadianceProbe(Vector2 position, int directionCount)
    {
        Position = position;
        DirectionCount = directionCount;
        Radiance = new Vector4[directionCount];
    }
}

public struct RadianceCascade
{
    public RadianceProbe[] Probes;
    public int ProbeCountX, ProbeCountY;
    public float ProbeSpacing;
    public float IntervalStart, IntervalLength;
    public int DirectionsPerProbe;

    public RadianceCascade(int probeCountX, int probeCountY, float probeSpacing,
                          float intervalStart, float intervalLength, int directionsPerProbe)
    {
        ProbeCountX = probeCountX;
        ProbeCountY = probeCountY;
        ProbeSpacing = probeSpacing;
        IntervalStart = intervalStart;
        IntervalLength = intervalLength;
        DirectionsPerProbe = directionsPerProbe;

        Probes = new RadianceProbe[probeCountX * probeCountY];

        // 初始化探针位置
        for (int y = 0; y < probeCountY; y++)
        {
            for (int x = 0; x < probeCountX; x++)
            {
                int index = y * probeCountX + x;
                Vector2 position = new Vector2(
                    x * probeSpacing + probeSpacing * 0.5f,
                    y * probeSpacing + probeSpacing * 0.5f
                );
                Probes[index] = new RadianceProbe(position, directionsPerProbe);
            }
        }
    }
}

public class RadianceCascades2D
{
    private RadianceCascade[] _cascades;
    private int _cascadeCount;
    private float _baseInterval;
    private int _baseDirections;
    private Vector2 _sceneSize;

    public RadianceCascades2D(Vector2 sceneSize, float baseInterval = 32f, int baseDirections = 16)
    {
        _sceneSize = sceneSize;
        _baseInterval = baseInterval;
        _baseDirections = baseDirections;

        // 计算需要的级联数量
        float diagonal = MathF.Sqrt(sceneSize.X * sceneSize.X + sceneSize.Y * sceneSize.Y);
        _cascadeCount = (int)MathF.Ceiling(MathF.Log(diagonal / baseInterval) / MathF.Log(4f)) + 1;
        _cascadeCount = Math.Max(1, Math.Min(_cascadeCount, 6)); // 限制在1-6个级联

        InitializeCascades();
    }

    private void InitializeCascades()
    {
        _cascades = new RadianceCascade[_cascadeCount];

        for (int i = 0; i < _cascadeCount; i++)
        {
            // 每个级联的探针数量减半，方向数量增加4倍
            int probeCountX = Math.Max(2, (int)(_sceneSize.X / _baseInterval) >> i);
            int probeCountY = Math.Max(2, (int)(_sceneSize.Y / _baseInterval) >> i);
            float probeSpacing = _sceneSize.X / probeCountX;

            // 计算间隔参数
            float intervalScale = MathF.Pow(4f, i);
            float intervalStart = i == 0 ? 0f : _baseInterval * (1f - intervalScale) / (1f - 4f);
            float intervalLength = _baseInterval * intervalScale;

            int directionsPerProbe = _baseDirections * (int)MathF.Pow(4f, i);

            _cascades[i] = new RadianceCascade(
                probeCountX, probeCountY, probeSpacing,
                intervalStart, intervalLength, directionsPerProbe
            );
        }
    }

    public RadianceCascade GetCascade(int index) => _cascades[index];
    public int CascadeCount => _cascadeCount;
}

class Program
{
    private const int SCREEN_WIDTH = 1280;
    private const int SCREEN_HEIGHT = 720;
    private static RadianceCascades2D _radianceCascades;
    private static RenderTexture2D _renderTarget;

    // 调试和交互变量
    private static bool _showProbes = false;
    private static bool _showRadiance = true;
    private static int _currentCascadeView = 0;
    private static float _lightIntensity = 1.0f;

    // 简单的SDF场景
    private static float CircleSDF(Vector2 point, Vector2 center, float radius)
    {
        return Vector2.Distance(point, center) - radius;
    }

    private static float BoxSDF(Vector2 point, Vector2 center, Vector2 size)
    {
        Vector2 d = Vector2.Abs(point - center) - size;
        return MathF.Max(d.X, d.Y);
    }

    private static float SceneSDF(Vector2 point)
    {
        // 创建一个简单的场景：几个圆形和矩形障碍物
        float circle1 = CircleSDF(point, new Vector2(300, 200), 50);
        float circle2 = CircleSDF(point, new Vector2(800, 400), 80);
        float box1 = BoxSDF(point, new Vector2(600, 200), new Vector2(60, 100));

        // 返回最近的距离（并集）
        return MathF.Min(MathF.Min(circle1, circle2), box1);
    }

    [STAThread]
    public static void Main()
    {
        Console.WriteLine("选择运行模式:");
        Console.WriteLine("1. 简化版 Radiance Cascades (推荐)");
        Console.WriteLine("2. 完整版 Radiance Cascades");
        Console.Write("请输入选择 (1 或 2): ");

        string? choice = Console.ReadLine();

        if (choice == "1" || string.IsNullOrEmpty(choice))
        {
            // 运行简化版本
            SimpleRadianceCascades.Run();
        }
        else
        {
            // 运行完整版本
            RunFullRadianceCascades();
        }
    }

    private static void RunFullRadianceCascades()
    {
        Raylib.InitWindow(SCREEN_WIDTH, SCREEN_HEIGHT, "Radiance Cascades 2D Demo");
        Raylib.SetTargetFPS(60);

        // 初始化Radiance Cascades系统
        _radianceCascades = new RadianceCascades2D(new Vector2(SCREEN_WIDTH, SCREEN_HEIGHT));
        _renderTarget = Raylib.LoadRenderTexture(SCREEN_WIDTH, SCREEN_HEIGHT);

        // 光源位置
        Vector2 lightPos = new Vector2(SCREEN_WIDTH / 2, SCREEN_HEIGHT / 2);
        Color lightColor = Color.Yellow;

        try
        {
            Console.WriteLine($"创建了 {_radianceCascades.CascadeCount} 个级联");

            int frameCount = 0;
            while (!Raylib.WindowShouldClose())
            {
                frameCount++;

                // 处理输入
                HandleInput(ref lightPos, ref lightColor);

                // 执行光线投射（降低频率以提高性能）
                if (frameCount % 3 == 0)
                {
                    UpdateRadianceCascades(lightPos, lightColor);
                }

                // 渲染
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.Black);

                // 渲染全局光照
                if (_showRadiance)
                {
                    RenderGlobalIllumination();
                }

                // 绘制场景几何体
                DrawScene();

                // 绘制光源
                Raylib.DrawCircleV(lightPos, 10, lightColor);

                // 绘制调试信息
                DrawDebugInfo();

                Raylib.EndDrawing();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"运行时错误: {ex.Message}");
            Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
        }
        finally
        {
            // 清理资源
            Raylib.UnloadRenderTexture(_renderTarget);
            Raylib.CloseWindow();
        }

        Console.WriteLine("程序结束，按任意键退出...");
        Console.ReadKey();
    }

    private static void HandleInput(ref Vector2 lightPos, ref Color lightColor)
    {
        // 鼠标控制光源位置
        if (Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            lightPos = Raylib.GetMousePosition();
        }

        // 键盘控制
        if (Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            _showProbes = !_showProbes;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.R))
        {
            _showRadiance = !_showRadiance;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.C))
        {
            _currentCascadeView = (_currentCascadeView + 1) % _radianceCascades.CascadeCount;
        }

        // 调整光照强度
        if (Raylib.IsKeyDown(KeyboardKey.Up))
        {
            _lightIntensity = Math.Min(3.0f, _lightIntensity + 0.02f);
        }
        if (Raylib.IsKeyDown(KeyboardKey.Down))
        {
            _lightIntensity = Math.Max(0.1f, _lightIntensity - 0.02f);
        }

        // 改变光源颜色
        if (Raylib.IsKeyPressed(KeyboardKey.One))
        {
            lightColor = Color.Red;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Two))
        {
            lightColor = Color.Green;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Three))
        {
            lightColor = Color.Blue;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Four))
        {
            lightColor = Color.Yellow;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Five))
        {
            lightColor = Color.White;
        }

        Raylib.UnloadRenderTexture(_renderTarget);
        Raylib.CloseWindow();
    }

    private static void UpdateRadianceCascades(Vector2 lightPos, Color lightColor)
    {
        // 第一步：为所有级联执行光线投射
        for (int cascadeIndex = 0; cascadeIndex < _radianceCascades.CascadeCount; cascadeIndex++)
        {
            var cascade = _radianceCascades.GetCascade(cascadeIndex);

            for (int probeIndex = 0; probeIndex < cascade.Probes.Length; probeIndex++)
            {
                ref var probe = ref cascade.Probes[probeIndex];

                // 为每个方向投射光线
                for (int dirIndex = 0; dirIndex < probe.DirectionCount; dirIndex++)
                {
                    float angle = 2f * MathF.PI * (dirIndex + 0.5f) / probe.DirectionCount;
                    Vector2 direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

                    // 光线投射
                    Vector4 radianceWithVisibility = CastRay(probe.Position, direction, cascade.IntervalStart,
                                                           cascade.IntervalLength, lightPos, lightColor);

                    probe.Radiance[dirIndex] = radianceWithVisibility;
                }
            }
        }

        // 第二步：从高级联向低级联合并
        MergeCascades();
    }

    private static void MergeCascades()
    {
        // 从最高级联开始，向下合并到Cascade 0
        for (int cascadeIndex = _radianceCascades.CascadeCount - 2; cascadeIndex >= 0; cascadeIndex--)
        {
            MergeCascadeDown(cascadeIndex + 1, cascadeIndex);
        }
    }

    private static void MergeCascadeDown(int sourceCascadeIndex, int targetCascadeIndex)
    {
        var sourceCascade = _radianceCascades.GetCascade(sourceCascadeIndex);
        var targetCascade = _radianceCascades.GetCascade(targetCascadeIndex);

        // 计算缩放比例
        float scaleX = (float)sourceCascade.ProbeCountX / targetCascade.ProbeCountX;
        float scaleY = (float)sourceCascade.ProbeCountY / targetCascade.ProbeCountY;
        int directionScale = targetCascade.DirectionsPerProbe / sourceCascade.DirectionsPerProbe;

        for (int targetProbeIndex = 0; targetProbeIndex < targetCascade.Probes.Length; targetProbeIndex++)
        {
            ref var targetProbe = ref targetCascade.Probes[targetProbeIndex];

            int targetX = targetProbeIndex % targetCascade.ProbeCountX;
            int targetY = targetProbeIndex / targetCascade.ProbeCountX;

            // 计算在源级联中的位置
            float sourceX = targetX * scaleX;
            float sourceY = targetY * scaleY;

            // 获取四个最近的源探针进行双线性插值
            int sourceX0 = (int)MathF.Floor(sourceX - 0.5f);
            int sourceY0 = (int)MathF.Floor(sourceY - 0.5f);
            int sourceX1 = sourceX0 + 1;
            int sourceY1 = sourceY0 + 1;

            float fx = sourceX - 0.5f - sourceX0;
            float fy = sourceY - 0.5f - sourceY0;

            // 合并每个方向
            for (int targetDirIndex = 0; targetDirIndex < targetProbe.DirectionCount; targetDirIndex++)
            {
                Vector4 mergedRadiance = Vector4.Zero;

                // 计算对应的源方向索引范围
                int sourceDirStart = targetDirIndex * directionScale;
                int sourceDirEnd = sourceDirStart + directionScale;

                // 双线性插值四个探针
                Vector4 interpolatedRadiance = Vector4.Zero;
                float totalWeight = 0f;

                for (int dy = 0; dy <= 1; dy++)
                {
                    for (int dx = 0; dx <= 1; dx++)
                    {
                        int sx = sourceX0 + dx;
                        int sy = sourceY0 + dy;

                        // 边界检查
                        if (sx >= 0 && sx < sourceCascade.ProbeCountX &&
                            sy >= 0 && sy < sourceCascade.ProbeCountY)
                        {
                            int sourceProbeIndex = sy * sourceCascade.ProbeCountX + sx;
                            var sourceProbe = sourceCascade.Probes[sourceProbeIndex];

                            float weight = (dx == 0 ? (1f - fx) : fx) * (dy == 0 ? (1f - fy) : fy);

                            // 合并对应方向的辐射度
                            Vector4 sourceRadianceSum = Vector4.Zero;
                            for (int sourceDirIndex = sourceDirStart; sourceDirIndex < sourceDirEnd &&
                                 sourceDirIndex < sourceProbe.DirectionCount; sourceDirIndex++)
                            {
                                sourceRadianceSum += sourceProbe.Radiance[sourceDirIndex];
                            }
                            sourceRadianceSum /= directionScale;

                            interpolatedRadiance += sourceRadianceSum * weight;
                            totalWeight += weight;
                        }
                    }
                }

                if (totalWeight > 0)
                {
                    interpolatedRadiance /= totalWeight;
                }

                // 合并间隔（考虑可见性）
                Vector4 currentRadiance = targetProbe.Radiance[targetDirIndex];
                Vector4 finalRadiance = MergeIntervals(currentRadiance, interpolatedRadiance);

                targetProbe.Radiance[targetDirIndex] = finalRadiance;
            }
        }
    }

    private static Vector4 MergeIntervals(Vector4 nearInterval, Vector4 farInterval)
    {
        // 近间隔的可见性影响远间隔
        Vector3 nearRadiance = new Vector3(nearInterval.X, nearInterval.Y, nearInterval.Z);
        Vector3 farRadiance = new Vector3(farInterval.X, farInterval.Y, farInterval.Z);
        float nearVisibility = nearInterval.W;
        float farVisibility = farInterval.W;

        // 合并辐射度：近 + 远*近的可见性
        Vector3 mergedRadiance = nearRadiance + farRadiance * nearVisibility;

        // 合并可见性
        float mergedVisibility = nearVisibility * farVisibility;

        return new Vector4(mergedRadiance, mergedVisibility);
    }

    private static Vector4 CastRay(Vector2 start, Vector2 direction, float intervalStart,
                                  float intervalLength, Vector2 lightPos, Color lightColor)
    {
        Vector2 rayStart = start + direction * intervalStart;
        Vector2 rayEnd = start + direction * (intervalStart + intervalLength);

        // 改进的光线步进，使用SDF加速
        Vector3 totalRadiance = Vector3.Zero;
        float visibility = 1.0f; // 可见性项
        Vector2 currentPos = rayStart;
        float totalDistance = 0f;
        float maxDistance = intervalLength;

        const float epsilon = 0.5f;
        const int maxSteps = 64;

        for (int step = 0; step < maxSteps && totalDistance < maxDistance; step++)
        {
            // 检查是否在屏幕范围内
            if (currentPos.X < 0 || currentPos.X >= SCREEN_WIDTH ||
                currentPos.Y < 0 || currentPos.Y >= SCREEN_HEIGHT)
            {
                visibility = 0.0f; // 射出屏幕外
                break;
            }

            float sdfDist = SceneSDF(currentPos);

            if (sdfDist <= epsilon)
            {
                // 击中表面
                visibility = 0.0f; // 被遮挡

                // 计算表面光照
                Vector2 surfaceNormal = CalculateNormal(currentPos);
                Vector2 lightDir = Vector2.Normalize(lightPos - currentPos);
                float NdotL = Math.Max(0, Vector2.Dot(surfaceNormal, lightDir));

                // 检查光源是否被遮挡（阴影）
                bool inShadow = IsInShadow(currentPos, lightPos);
                if (!inShadow)
                {
                    float distToLight = Vector2.Distance(currentPos, lightPos);
                    float attenuation = 1.0f / (1.0f + distToLight * 0.0005f);

                    totalRadiance = new Vector3(
                        lightColor.R / 255.0f * attenuation * NdotL * _lightIntensity,
                        lightColor.G / 255.0f * attenuation * NdotL * _lightIntensity,
                        lightColor.B / 255.0f * attenuation * NdotL * _lightIntensity
                    );
                }
                break;
            }

            // SDF步进
            float stepSize = Math.Max(1.0f, sdfDist * 0.8f);
            currentPos += direction * stepSize;
            totalDistance += stepSize;
        }

        return new Vector4(totalRadiance, visibility);
    }

    private static Vector2 CalculateNormal(Vector2 point)
    {
        const float h = 1.0f;
        float dx = SceneSDF(new Vector2(point.X + h, point.Y)) - SceneSDF(new Vector2(point.X - h, point.Y));
        float dy = SceneSDF(new Vector2(point.X, point.Y + h)) - SceneSDF(new Vector2(point.X, point.Y - h));
        return Vector2.Normalize(new Vector2(dx, dy));
    }

    private static bool IsInShadow(Vector2 point, Vector2 lightPos)
    {
        Vector2 lightDir = lightPos - point;
        float lightDistance = lightDir.Length();
        lightDir = Vector2.Normalize(lightDir);

        Vector2 currentPos = point + lightDir * 2.0f; // 稍微偏移避免自阴影
        float totalDistance = 2.0f;

        const int maxSteps = 32;
        for (int step = 0; step < maxSteps && totalDistance < lightDistance; step++)
        {
            float sdfDist = SceneSDF(currentPos);
            if (sdfDist <= 0.5f)
                return true; // 在阴影中

            float stepSize = Math.Max(1.0f, sdfDist * 0.8f);
            currentPos += lightDir * stepSize;
            totalDistance += stepSize;
        }

        return false; // 不在阴影中
    }

    private static void RenderGlobalIllumination()
    {
        // 从Cascade 0提取全局光照信息并渲染
        var cascade0 = _radianceCascades.GetCascade(0);

        // 为屏幕上的每个像素计算光照
        int step = 4; // 降低分辨率以提高性能

        for (int y = 0; y < SCREEN_HEIGHT; y += step)
        {
            for (int x = 0; x < SCREEN_WIDTH; x += step)
            {
                Vector2 pixelPos = new Vector2(x, y);
                Vector3 irradiance = SampleIrradiance(pixelPos, cascade0);

                // 将辐射度转换为颜色
                Color pixelColor = new Color(
                    (int)Math.Min(255, irradiance.X * 255),
                    (int)Math.Min(255, irradiance.Y * 255),
                    (int)Math.Min(255, irradiance.Z * 255),
                    128 // 半透明以便看到几何体
                );

                // 绘制像素块
                if (pixelColor.R > 5 || pixelColor.G > 5 || pixelColor.B > 5) // 只绘制有光照的区域
                {
                    Raylib.DrawRectangle(x, y, step, step, pixelColor);
                }
            }
        }
    }

    private static Vector3 SampleIrradiance(Vector2 position, RadianceCascade cascade)
    {
        // 找到最近的四个探针进行双线性插值
        float probeX = position.X / cascade.ProbeSpacing - 0.5f;
        float probeY = position.Y / cascade.ProbeSpacing - 0.5f;

        int probeX0 = (int)MathF.Floor(probeX);
        int probeY0 = (int)MathF.Floor(probeY);
        int probeX1 = probeX0 + 1;
        int probeY1 = probeY0 + 1;

        float fx = probeX - probeX0;
        float fy = probeY - probeY0;

        Vector3 totalIrradiance = Vector3.Zero;
        float totalWeight = 0f;

        // 双线性插值四个探针
        for (int dy = 0; dy <= 1; dy++)
        {
            for (int dx = 0; dx <= 1; dx++)
            {
                int px = probeX0 + dx;
                int py = probeY0 + dy;

                // 边界检查
                if (px >= 0 && px < cascade.ProbeCountX && py >= 0 && py < cascade.ProbeCountY)
                {
                    int probeIndex = py * cascade.ProbeCountX + px;
                    var probe = cascade.Probes[probeIndex];

                    float weight = (dx == 0 ? (1f - fx) : fx) * (dy == 0 ? (1f - fy) : fy);

                    // 计算该探针的总辐射度（所有方向的积分）
                    Vector3 probeIrradiance = Vector3.Zero;
                    for (int dirIndex = 0; dirIndex < probe.DirectionCount; dirIndex++)
                    {
                        var radiance = probe.Radiance[dirIndex];
                        probeIrradiance += new Vector3(radiance.X, radiance.Y, radiance.Z);
                    }
                    probeIrradiance /= probe.DirectionCount; // 平均

                    totalIrradiance += probeIrradiance * weight;
                    totalWeight += weight;
                }
            }
        }

        if (totalWeight > 0)
        {
            totalIrradiance /= totalWeight;
        }

        return totalIrradiance;
    }

    private static void DrawScene()
    {
        // 绘制场景中的几何体（半透明以便看到光照）
        Color geometryColor = new Color(100, 100, 100, 180);
        Raylib.DrawCircle(300, 200, 50, geometryColor);
        Raylib.DrawCircle(800, 400, 80, geometryColor);
        Raylib.DrawRectangle(540, 100, 120, 200, geometryColor);
    }

    private static void DrawDebugInfo()
    {
        // 绘制级联信息
        int yOffset = 10;
        Raylib.DrawText($"Radiance Cascades 2D Demo", 10, yOffset, 20, Color.White);
        yOffset += 25;

        Raylib.DrawText($"Cascades: {_radianceCascades.CascadeCount}", 10, yOffset, 16, Color.White);
        yOffset += 20;

        for (int i = 0; i < _radianceCascades.CascadeCount; i++)
        {
            var cascade = _radianceCascades.GetCascade(i);
            Color cascadeColor = i == _currentCascadeView ? Color.Yellow : Color.LightGray;
            Raylib.DrawText($"C{i}: {cascade.ProbeCountX}x{cascade.ProbeCountY} probes, " +
                          $"{cascade.DirectionsPerProbe} dirs", 10, yOffset, 14, cascadeColor);
            yOffset += 18;
        }

        yOffset += 10;
        Raylib.DrawText($"Light Intensity: {_lightIntensity:F2}", 10, yOffset, 14, Color.White);
        yOffset += 18;
        Raylib.DrawText($"Show Radiance: {(_showRadiance ? "ON" : "OFF")}", 10, yOffset, 14, Color.White);
        yOffset += 18;
        Raylib.DrawText($"Current Cascade View: {_currentCascadeView}", 10, yOffset, 14, Color.White);

        // 绘制探针位置
        if (_showProbes)
        {
            var cascade = _radianceCascades.GetCascade(_currentCascadeView);
            for (int i = 0; i < cascade.Probes.Length; i++)
            {
                var probe = cascade.Probes[i];
                Raylib.DrawCircleV(probe.Position, 3, Color.Red);

                // 可选：绘制探针的光线方向（仅显示少数几个）
                if (i % 4 == 0) // 只显示部分探针的光线
                {
                    for (int dirIndex = 0; dirIndex < Math.Min(8, probe.DirectionCount); dirIndex += 2)
                    {
                        float angle = 2f * MathF.PI * (dirIndex + 0.5f) / probe.DirectionCount;
                        Vector2 direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                        Vector2 endPos = probe.Position + direction * 20;

                        var radiance = probe.Radiance[dirIndex];
                        float intensity = (radiance.X + radiance.Y + radiance.Z) / 3f;
                        if (intensity > 0.01f)
                        {
                            Color rayColor = new Color(255, 255, 0, (int)(intensity * 255));
                            Raylib.DrawLineV(probe.Position, endPos, rayColor);
                        }
                    }
                }
            }
        }

        // 控制说明
        yOffset = SCREEN_HEIGHT - 120;
        Raylib.DrawText("Controls:", 10, yOffset, 16, Color.Yellow);
        yOffset += 20;
        Raylib.DrawText("Left Click: Move light", 10, yOffset, 12, Color.Green);
        yOffset += 15;
        Raylib.DrawText("SPACE: Toggle probes", 10, yOffset, 12, Color.Green);
        yOffset += 15;
        Raylib.DrawText("R: Toggle radiance", 10, yOffset, 12, Color.Green);
        yOffset += 15;
        Raylib.DrawText("C: Cycle cascade view", 10, yOffset, 12, Color.Green);
        yOffset += 15;
        Raylib.DrawText("Up/Down: Light intensity", 10, yOffset, 12, Color.Green);
        yOffset += 15;
        Raylib.DrawText("1-5: Change light color", 10, yOffset, 12, Color.Green);
    }
}