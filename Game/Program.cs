using Raylib_cs;
using System.Numerics;

namespace Game;

// 2D Light structure
public struct Light2D
{
    public Vector2 Position;
    public Color Color;
    public float Radius;
    public float Intensity;

    public Light2D(Vector2 position, Color color, float radius, float intensity = 1.0f)
    {
        Position = position;
        Color = color;
        Radius = radius;
        Intensity = intensity;
    }
}

// 2D Sprite with normal map support
public struct Sprite2D
{
    public Vector2 Position;
    public Vector2 Size;
    public Texture2D DiffuseTexture;
    public Texture2D NormalTexture;
    public Color Tint;

    public Sprite2D(Vector2 position, Vector2 size, Texture2D diffuse, Texture2D normal, Color tint)
    {
        Position = position;
        Size = size;
        DiffuseTexture = diffuse;
        NormalTexture = normal;
        Tint = tint;
    }
}

class Program
{
    private const int SCREEN_WIDTH = 1024;
    private const int SCREEN_HEIGHT = 768;

    // Render targets for deferred lighting
    private static RenderTexture2D gBufferDiffuse;
    private static RenderTexture2D gBufferNormal;
    private static RenderTexture2D lightBuffer;
    private static RenderTexture2D fogBuffer;
    private static RenderTexture2D volumetricBuffer;

    // Shaders
    private static Shader geometryShader;
    private static Shader lightingShader;
    private static Shader compositeShader;
    private static Shader volumetricFogShader;
    private static Shader fogCompositeShader;

    // Scene data
    private static List<Sprite2D> sprites = new();
    private static List<Light2D> lights = new();

    // Test textures
    private static Texture2D testDiffuse;
    private static Texture2D testNormal;

    [STAThread]
    public static void Main()
    {
        Raylib.InitWindow(SCREEN_WIDTH, SCREEN_HEIGHT, "2D Deferred Lighting Demo");
        Raylib.SetTargetFPS(60);

        InitializeDeferredLighting();
        LoadTestAssets();
        SetupScene();

        while (!Raylib.WindowShouldClose())
        {
            UpdateScene();
            RenderDeferredLighting();
        }

        CleanupDeferredLighting();
        Raylib.CloseWindow();
    }

    private static void InitializeDeferredLighting()
    {
        // Create G-Buffer render targets
        gBufferDiffuse = Raylib.LoadRenderTexture(SCREEN_WIDTH, SCREEN_HEIGHT);
        gBufferNormal = Raylib.LoadRenderTexture(SCREEN_WIDTH, SCREEN_HEIGHT);
        lightBuffer = Raylib.LoadRenderTexture(SCREEN_WIDTH, SCREEN_HEIGHT);

        // Create volumetric fog render targets
        fogBuffer = Raylib.LoadRenderTexture(SCREEN_WIDTH / 2, SCREEN_HEIGHT / 2); // Lower resolution for performance
        volumetricBuffer = Raylib.LoadRenderTexture(SCREEN_WIDTH, SCREEN_HEIGHT);

        // Load shaders
        geometryShader = LoadGeometryShader();
        lightingShader = LoadLightingShader();
        compositeShader = LoadCompositeShader();
        volumetricFogShader = LoadVolumetricFogShader();
        fogCompositeShader = LoadFogCompositeShader();
    }

    private static void LoadTestAssets()
    {
        // Create simple test textures
        testDiffuse = CreateTestDiffuseTexture();
        testNormal = CreateTestNormalTexture();
    }

    private static void SetupScene()
    {
        // Add some test sprites
        sprites.Add(new Sprite2D(
            new Vector2(200, 200),
            new Vector2(128, 128),
            testDiffuse,
            testNormal,
            Color.White
        ));

        sprites.Add(new Sprite2D(
            new Vector2(400, 300),
            new Vector2(128, 128),
            testDiffuse,
            testNormal,
            Color.White
        ));

        sprites.Add(new Sprite2D(
            new Vector2(600, 150),
            new Vector2(128, 128),
            testDiffuse,
            testNormal,
            Color.White
        ));

        // Add some lights
        lights.Add(new Light2D(new Vector2(300, 250), Color.Red, 200.0f, 5.5f));
        lights.Add(new Light2D(new Vector2(500, 400), Color.Blue, 150.0f, 4.2f));
        lights.Add(new Light2D(new Vector2(700, 200), Color.Green, 180.0f, 3.0f));
    }

    private static void UpdateScene()
    {
        // Make lights follow mouse for demo
        Vector2 mousePos = Raylib.GetMousePosition();
        if (lights.Count > 0)
        {
            var light = lights[0];
            light.Position = mousePos;
            lights[0] = light;
        }

        // Animate other lights
        float time = (float)Raylib.GetTime();
        for (int i = 1; i < lights.Count; i++)
        {
            var light = lights[i];
            light.Position = new Vector2(
                light.Position.X + MathF.Sin(time + i) * 2.0f,
                light.Position.Y + MathF.Cos(time + i * 0.7f) * 1.5f
            );
            lights[i] = light;
        }
    }

    private static void RenderDeferredLighting()
    {
        // Phase 1: Geometry Pass - Render to G-Buffer
        RenderGeometryPass();

        // Phase 2: Lighting Pass - Accumulate lights
        RenderLightingPass();

        // Phase 3: Volumetric Fog Pass - Create fog effects
        RenderVolumetricFogPass();

        // Phase 4: Composite Pass - Final output with fog
        RenderCompositePass();
    }

    private static void RenderGeometryPass()
    {
        // Render diffuse to first G-Buffer
        Raylib.BeginTextureMode(gBufferDiffuse);
        Raylib.ClearBackground(Color.Black);

        foreach (var sprite in sprites)
        {
            Rectangle sourceRec = new Rectangle(0, 0, sprite.DiffuseTexture.Width, sprite.DiffuseTexture.Height);
            Rectangle destRec = new Rectangle(sprite.Position.X, sprite.Position.Y, sprite.Size.X, sprite.Size.Y);
            Raylib.DrawTexturePro(sprite.DiffuseTexture, sourceRec, destRec, Vector2.Zero, 0.0f, sprite.Tint);
        }

        Raylib.EndTextureMode();

        // Render normals to second G-Buffer
        Raylib.BeginTextureMode(gBufferNormal);
        Raylib.ClearBackground(new Color(128, 128, 255, 255)); // Default normal (0, 0, 1) in RGB

        foreach (var sprite in sprites)
        {
            Rectangle sourceRec = new Rectangle(0, 0, sprite.NormalTexture.Width, sprite.NormalTexture.Height);
            Rectangle destRec = new Rectangle(sprite.Position.X, sprite.Position.Y, sprite.Size.X, sprite.Size.Y);
            Raylib.DrawTexturePro(sprite.NormalTexture, sourceRec, destRec, Vector2.Zero, 0.0f, Color.White);
        }

        Raylib.EndTextureMode();
    }

    private static void RenderLightingPass()
    {
        Raylib.BeginTextureMode(lightBuffer);
        Raylib.ClearBackground(Color.Black);

        // Enable additive blending for light accumulation
        Raylib.BeginBlendMode(BlendMode.Additive);

        foreach (var light in lights)
        {
            RenderLight(light);
        }

        Raylib.EndBlendMode();
        Raylib.EndTextureMode();
    }

    private static void RenderLight(Light2D light)
    {
        // Use lighting shader
        Raylib.BeginShaderMode(lightingShader);

        // Set shader uniforms
        int lightPosLoc = Raylib.GetShaderLocation(lightingShader, "lightPos");
        int lightColorLoc = Raylib.GetShaderLocation(lightingShader, "lightColor");
        int lightRadiusLoc = Raylib.GetShaderLocation(lightingShader, "lightRadius");
        int lightIntensityLoc = Raylib.GetShaderLocation(lightingShader, "lightIntensity");
        int screenSizeLoc = Raylib.GetShaderLocation(lightingShader, "screenSize");

        Raylib.SetShaderValue(lightingShader, lightPosLoc, light.Position, ShaderUniformDataType.Vec2);
        Vector3 lightColorVec = new Vector3(light.Color.R / 255.0f, light.Color.G / 255.0f, light.Color.B / 255.0f);
        Raylib.SetShaderValue(lightingShader, lightColorLoc, lightColorVec, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(lightingShader, lightRadiusLoc, light.Radius, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(lightingShader, lightIntensityLoc, light.Intensity, ShaderUniformDataType.Float);
        Vector2 screenSize = new Vector2(SCREEN_WIDTH, SCREEN_HEIGHT);
        Raylib.SetShaderValue(lightingShader, screenSizeLoc, screenSize, ShaderUniformDataType.Vec2);

        // Set G-Buffer textures
        int diffuseTexLoc = Raylib.GetShaderLocation(lightingShader, "diffuseTexture");
        int normalTexLoc = Raylib.GetShaderLocation(lightingShader, "normalTexture");

        Raylib.SetShaderValueTexture(lightingShader, diffuseTexLoc, gBufferDiffuse.Texture);
        Raylib.SetShaderValueTexture(lightingShader, normalTexLoc, gBufferNormal.Texture);

        // Draw full-screen quad for this light
        Rectangle lightArea = new Rectangle(
            light.Position.X - light.Radius,
            light.Position.Y - light.Radius,
            light.Radius * 2,
            light.Radius * 2
        );

        Raylib.DrawRectangleRec(lightArea, Color.White);

        Raylib.EndShaderMode();
    }

    private static void RenderVolumetricFogPass()
    {
        // First, render fog density at lower resolution for performance
        Raylib.BeginTextureMode(fogBuffer);
        Raylib.ClearBackground(Color.Black);

        Raylib.BeginShaderMode(volumetricFogShader);

        // Set fog shader uniforms
        int timeLoc = Raylib.GetShaderLocation(volumetricFogShader, "time");
        int screenSizeLoc = Raylib.GetShaderLocation(volumetricFogShader, "screenSize");
        int lightCountLoc = Raylib.GetShaderLocation(volumetricFogShader, "lightCount");

        float time = (float)Raylib.GetTime();
        Vector2 fogScreenSize = new Vector2(fogBuffer.Texture.Width, fogBuffer.Texture.Height);
        int lightCount = lights.Count;

        Raylib.SetShaderValue(volumetricFogShader, timeLoc, time, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(volumetricFogShader, screenSizeLoc, fogScreenSize, ShaderUniformDataType.Vec2);
        Raylib.SetShaderValue(volumetricFogShader, lightCountLoc, lightCount, ShaderUniformDataType.Int);

        // Pass light positions and colors
        for (int i = 0; i < Math.Min(lights.Count, 8); i++) // Limit to 8 lights for performance
        {
            var light = lights[i];
            int lightPosLoc = Raylib.GetShaderLocation(volumetricFogShader, $"lights[{i}].position");
            int lightColorLoc = Raylib.GetShaderLocation(volumetricFogShader, $"lights[{i}].color");
            int lightRadiusLoc = Raylib.GetShaderLocation(volumetricFogShader, $"lights[{i}].radius");
            int lightIntensityLoc = Raylib.GetShaderLocation(volumetricFogShader, $"lights[{i}].intensity");

            // Scale light position to fog buffer resolution
            Vector2 scaledPos = new Vector2(
                light.Position.X * fogScreenSize.X / SCREEN_WIDTH,
                light.Position.Y * fogScreenSize.Y / SCREEN_HEIGHT
            );

            Vector3 lightColorVec = new Vector3(light.Color.R / 255.0f, light.Color.G / 255.0f, light.Color.B / 255.0f);
            float scaledRadius = light.Radius * fogScreenSize.X / SCREEN_WIDTH;

            Raylib.SetShaderValue(volumetricFogShader, lightPosLoc, scaledPos, ShaderUniformDataType.Vec2);
            Raylib.SetShaderValue(volumetricFogShader, lightColorLoc, lightColorVec, ShaderUniformDataType.Vec3);
            Raylib.SetShaderValue(volumetricFogShader, lightRadiusLoc, scaledRadius, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(volumetricFogShader, lightIntensityLoc, light.Intensity, ShaderUniformDataType.Float);
        }

        // Draw full-screen quad to generate fog
        Rectangle fogRect = new Rectangle(0, 0, fogBuffer.Texture.Width, fogBuffer.Texture.Height);
        Raylib.DrawRectangleRec(fogRect, Color.White);

        Raylib.EndShaderMode();
        Raylib.EndTextureMode();

        // Now upscale and blur the fog
        Raylib.BeginTextureMode(volumetricBuffer);
        Raylib.ClearBackground(Color.Black);

        // Simple upscale with some blur
        Rectangle sourceRec = new Rectangle(0, 0, fogBuffer.Texture.Width, -fogBuffer.Texture.Height);
        Rectangle destRec = new Rectangle(0, 0, SCREEN_WIDTH, SCREEN_HEIGHT);
        Raylib.DrawTexturePro(fogBuffer.Texture, sourceRec, destRec, Vector2.Zero, 0.0f, Color.White);

        Raylib.EndTextureMode();
    }

    private static void RenderCompositePass()
    {
        // Final composite to screen
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);

        Raylib.BeginShaderMode(fogCompositeShader);

        // Set fog composite shader uniforms
        int diffuseTexLoc = Raylib.GetShaderLocation(fogCompositeShader, "diffuseTexture");
        int lightTexLoc = Raylib.GetShaderLocation(fogCompositeShader, "lightTexture");
        int fogTexLoc = Raylib.GetShaderLocation(fogCompositeShader, "fogTexture");
        int fogIntensityLoc = Raylib.GetShaderLocation(fogCompositeShader, "fogIntensity");
        int fogColorLoc = Raylib.GetShaderLocation(fogCompositeShader, "fogColor");

        Raylib.SetShaderValueTexture(fogCompositeShader, diffuseTexLoc, gBufferDiffuse.Texture);
        Raylib.SetShaderValueTexture(fogCompositeShader, lightTexLoc, lightBuffer.Texture);
        Raylib.SetShaderValueTexture(fogCompositeShader, fogTexLoc, volumetricBuffer.Texture);

        // Fog parameters
        float fogIntensity = 0.8f;
        Vector3 fogColor = new Vector3(0.7f, 0.8f, 1.0f); // Light blue fog

        Raylib.SetShaderValue(fogCompositeShader, fogIntensityLoc, fogIntensity, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(fogCompositeShader, fogColorLoc, fogColor, ShaderUniformDataType.Vec3);

        // Draw full-screen quad
        Rectangle sourceRec = new Rectangle(0, 0, SCREEN_WIDTH, -SCREEN_HEIGHT); // Flip Y
        Rectangle destRec = new Rectangle(0, 0, SCREEN_WIDTH, SCREEN_HEIGHT);
        Raylib.DrawTexturePro(gBufferDiffuse.Texture, sourceRec, destRec, Vector2.Zero, 0.0f, Color.White);

        Raylib.EndShaderMode();

        // Draw debug info
        Raylib.DrawText($"Lights: {lights.Count}", 10, 10, 20, Color.White);
        Raylib.DrawText($"Sprites: {sprites.Count}", 10, 35, 20, Color.White);
        Raylib.DrawText("Move mouse to control red light", 10, 60, 20, Color.White);
        Raylib.DrawText("2D Volumetric Fog Demo", 10, 85, 20, Color.White);

        Raylib.EndDrawing();
    }

    private static void CleanupDeferredLighting()
    {
        Raylib.UnloadRenderTexture(gBufferDiffuse);
        Raylib.UnloadRenderTexture(gBufferNormal);
        Raylib.UnloadRenderTexture(lightBuffer);
        Raylib.UnloadRenderTexture(fogBuffer);
        Raylib.UnloadRenderTexture(volumetricBuffer);

        Raylib.UnloadShader(geometryShader);
        Raylib.UnloadShader(lightingShader);
        Raylib.UnloadShader(compositeShader);
        Raylib.UnloadShader(volumetricFogShader);
        Raylib.UnloadShader(fogCompositeShader);

        Raylib.UnloadTexture(testDiffuse);
        Raylib.UnloadTexture(testNormal);
    }

    private static Shader LoadGeometryShader()
    {
        // Simple pass-through shader for geometry
        string vertexShader = @"
#version 330
in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec3 vertexNormal;
in vec4 vertexColor;

uniform mat4 mvp;

out vec2 fragTexCoord;
out vec4 fragColor;

void main()
{
    fragTexCoord = vertexTexCoord;
    fragColor = vertexColor;
    gl_Position = mvp*vec4(vertexPosition, 1.0);
}";

        string fragmentShader = @"
#version 330
in vec2 fragTexCoord;
in vec4 fragColor;

uniform sampler2D texture0;
uniform vec4 colDiffuse;

out vec4 finalColor;

void main()
{
    vec4 texelColor = texture(texture0, fragTexCoord);
    finalColor = texelColor*colDiffuse*fragColor;
}";

        return Raylib.LoadShaderFromMemory(vertexShader, fragmentShader);
    }

    private static Shader LoadLightingShader()
    {
        string vertexShader = @"
#version 330
in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec3 vertexNormal;
in vec4 vertexColor;

uniform mat4 mvp;

out vec2 fragTexCoord;
out vec4 fragColor;

void main()
{
    fragTexCoord = vertexTexCoord;
    fragColor = vertexColor;
    gl_Position = mvp*vec4(vertexPosition, 1.0);
}";

        string fragmentShader = @"
#version 330
in vec2 fragTexCoord;
in vec4 fragColor;

uniform sampler2D diffuseTexture;
uniform sampler2D normalTexture;
uniform vec2 lightPos;
uniform vec3 lightColor;
uniform float lightRadius;
uniform float lightIntensity;
uniform vec2 screenSize;

out vec4 finalColor;

void main()
{
    vec2 screenCoord = gl_FragCoord.xy;
    vec2 lightDir = lightPos - screenCoord;
    float distance = length(lightDir);

    // Early exit if outside light radius
    if (distance > lightRadius) {
        finalColor = vec4(0.0, 0.0, 0.0, 1.0);
        return;
    }

    // Normalize light direction
    lightDir = normalize(lightDir);

    // Sample normal from G-Buffer
    vec2 texCoord = screenCoord / screenSize;
    texCoord.y = 1.0 - texCoord.y; // Flip Y coordinate
    vec3 normal = texture(normalTexture, texCoord).rgb;
    normal = normalize(normal * 2.0 - 1.0); // Convert from [0,1] to [-1,1]

    // Sample diffuse color
    vec3 diffuse = texture(diffuseTexture, texCoord).rgb;

    // Calculate lighting
    float attenuation = 1.0 - (distance / lightRadius);
    attenuation = attenuation * attenuation; // Quadratic falloff

    float NdotL = max(dot(normal, vec3(lightDir, 0.0)), 0.0);

    vec3 lighting = lightColor * lightIntensity * attenuation * NdotL;

    finalColor = vec4(diffuse * lighting, 1.0);
}";

        return Raylib.LoadShaderFromMemory(vertexShader, fragmentShader);
    }

    private static Shader LoadCompositeShader()
    {
        string vertexShader = @"
#version 330
in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec3 vertexNormal;
in vec4 vertexColor;

uniform mat4 mvp;

out vec2 fragTexCoord;
out vec4 fragColor;

void main()
{
    fragTexCoord = vertexTexCoord;
    fragColor = vertexColor;
    gl_Position = mvp*vec4(vertexPosition, 1.0);
}";

        string fragmentShader = @"
#version 330
in vec2 fragTexCoord;
in vec4 fragColor;

uniform sampler2D diffuseTexture;
uniform sampler2D lightTexture;

out vec4 finalColor;

void main()
{
    vec3 diffuse = texture(diffuseTexture, fragTexCoord).rgb;
    vec3 lighting = texture(lightTexture, fragTexCoord).rgb;

    // Add ambient lighting
    vec3 ambient = diffuse * 0.1;

    // Combine diffuse and lighting
    vec3 result = ambient + lighting;

    finalColor = vec4(result, 1.0);
}";

        return Raylib.LoadShaderFromMemory(vertexShader, fragmentShader);
    }

    private static Texture2D CreateTestDiffuseTexture()
    {
        const int size = 128;
        Image image = Raylib.GenImageColor(size, size, Color.White);

        // Create a simple pattern
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Create a checkerboard pattern with some variation
                bool checker = ((x / 16) + (y / 16)) % 2 == 0;
                Color color = checker ? new Color(200, 150, 100, 255) : new Color(150, 100, 200, 255);

                // Add some noise for texture
                int noise = (int)(MathF.Sin(x * 0.1f) * MathF.Cos(y * 0.1f) * 30);
                color.R = (byte)Math.Clamp(color.R + noise, 0, 255);
                color.G = (byte)Math.Clamp(color.G + noise, 0, 255);
                color.B = (byte)Math.Clamp(color.B + noise, 0, 255);

                Raylib.ImageDrawPixel(ref image, x, y, color);
            }
        }

        Texture2D texture = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);
        return texture;
    }

    private static Texture2D CreateTestNormalTexture()
    {
        const int size = 128;
        Image image = Raylib.GenImageColor(size, size, new Color(128, 128, 255, 255)); // Default normal

        // Create some bumps and details
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Create circular bumps
                float centerX = size * 0.5f;
                float centerY = size * 0.5f;
                float dist = MathF.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));

                // Create normal map based on distance from center
                float height = MathF.Max(0, 1.0f - (dist / (size * 0.3f)));
                height = height * height; // Smooth falloff

                // Calculate normal from height
                float heightL = x > 0 ? GetHeightAt(x - 1, y, size) : height;
                float heightR = x < size - 1 ? GetHeightAt(x + 1, y, size) : height;
                float heightU = y > 0 ? GetHeightAt(x, y - 1, size) : height;
                float heightD = y < size - 1 ? GetHeightAt(x, y + 1, size) : height;

                Vector3 normal = Vector3.Normalize(new Vector3(heightL - heightR, heightU - heightD, 1.0f));

                // Convert normal to color ([-1,1] to [0,255])
                Color normalColor = new Color(
                    (int)((normal.X * 0.5f + 0.5f) * 255),
                    (int)((normal.Y * 0.5f + 0.5f) * 255),
                    (int)((normal.Z * 0.5f + 0.5f) * 255),
                    255
                );

                Raylib.ImageDrawPixel(ref image, x, y, normalColor);
            }
        }

        Texture2D texture = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);
        return texture;
    }

    private static float GetHeightAt(int x, int y, int size)
    {
        float centerX = size * 0.5f;
        float centerY = size * 0.5f;
        float dist = MathF.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));

        float height = MathF.Max(0, 1.0f - (dist / (size * 0.3f)));
        return height * height;
    }

    private static Shader LoadVolumetricFogShader()
    {
        string vertexShader = @"
#version 330
in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec3 vertexNormal;
in vec4 vertexColor;

uniform mat4 mvp;

out vec2 fragTexCoord;
out vec4 fragColor;

void main()
{
    fragTexCoord = vertexTexCoord;
    fragColor = vertexColor;
    gl_Position = mvp*vec4(vertexPosition, 1.0);
}";

        string fragmentShader = @"
#version 330
in vec2 fragTexCoord;
in vec4 fragColor;

uniform float time;
uniform vec2 screenSize;
uniform int lightCount;

// Light structure
struct Light {
    vec2 position;
    vec3 color;
    float radius;
    float intensity;
};

uniform Light lights[8];

out vec4 finalColor;

// Noise function for fog density variation
float noise(vec2 p) {
    return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

// Smooth noise
float smoothNoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = noise(i);
    float b = noise(i + vec2(1.0, 0.0));
    float c = noise(i + vec2(0.0, 1.0));
    float d = noise(i + vec2(1.0, 1.0));

    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

// Fractal noise for more complex fog patterns
float fractalNoise(vec2 p) {
    float value = 0.0;
    float amplitude = 0.5;

    for (int i = 0; i < 4; i++) {
        value += amplitude * smoothNoise(p);
        p *= 2.0;
        amplitude *= 0.5;
    }

    return value;
}

void main()
{
    vec2 screenCoord = gl_FragCoord.xy;
    vec2 uv = screenCoord / screenSize;

    // Base fog density with animated noise
    vec2 fogCoord = uv * 8.0 + vec2(time * 0.1, time * 0.05);
    float baseFogDensity = fractalNoise(fogCoord) * 0.3;

    // Add some vertical gradient (more fog at bottom)
    baseFogDensity += (1.0 - uv.y) * 0.2;

    // Calculate volumetric lighting from all lights
    vec3 volumetricLight = vec3(0.0);

    for (int i = 0; i < min(lightCount, 8); i++) {
        vec2 lightDir = lights[i].position - screenCoord;
        float distance = length(lightDir);

        if (distance < lights[i].radius) {
            // Light attenuation
            float attenuation = 1.0 - (distance / lights[i].radius);
            attenuation = attenuation * attenuation;

            // Volumetric scattering - simulate light rays through fog
            float scattering = attenuation * lights[i].intensity;

            // Add some ray-marching effect
            vec2 rayDir = normalize(lightDir);
            float rayLength = distance;
            int steps = 16;
            float stepSize = rayLength / float(steps);

            float volumetricContribution = 0.0;
            for (int j = 0; j < steps; j++) {
                vec2 samplePos = screenCoord + rayDir * stepSize * float(j);
                vec2 sampleUV = samplePos / screenSize;

                // Sample fog density along the ray
                vec2 sampleFogCoord = sampleUV * 8.0 + vec2(time * 0.1, time * 0.05);
                float sampleDensity = fractalNoise(sampleFogCoord) * 0.5 + 0.3;

                volumetricContribution += sampleDensity * (1.0 / float(steps));
            }

            volumetricLight += lights[i].color * scattering * volumetricContribution;
        }
    }

    // Combine base fog density with volumetric lighting
    float finalDensity = baseFogDensity + length(volumetricLight) * 0.5;
    finalDensity = clamp(finalDensity, 0.0, 1.0);

    // Output fog with volumetric lighting color
    vec3 fogColor = mix(vec3(0.1, 0.1, 0.2), volumetricLight, 0.7);
    finalColor = vec4(fogColor, finalDensity);
}";

        return Raylib.LoadShaderFromMemory(vertexShader, fragmentShader);
    }

    private static Shader LoadFogCompositeShader()
    {
        string vertexShader = @"
#version 330
in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec3 vertexNormal;
in vec4 vertexColor;

uniform mat4 mvp;

out vec2 fragTexCoord;
out vec4 fragColor;

void main()
{
    fragTexCoord = vertexTexCoord;
    fragColor = vertexColor;
    gl_Position = mvp*vec4(vertexPosition, 1.0);
}";

        string fragmentShader = @"
#version 330
in vec2 fragTexCoord;
in vec4 fragColor;

uniform sampler2D diffuseTexture;
uniform sampler2D lightTexture;
uniform sampler2D fogTexture;
uniform float fogIntensity;
uniform vec3 fogColor;

out vec4 finalColor;

void main()
{
    vec3 diffuse = texture(diffuseTexture, fragTexCoord).rgb;
    vec3 lighting = texture(lightTexture, fragTexCoord).rgb;
    vec4 fog = texture(fogTexture, fragTexCoord);

    // Add ambient lighting
    vec3 ambient = diffuse * 0.1;

    // Combine diffuse and lighting
    vec3 litScene = ambient + lighting;

    // Apply volumetric fog
    vec3 fogContribution = fog.rgb * fogIntensity;
    float fogAlpha = fog.a * fogIntensity;

    // Blend fog with scene
    vec3 result = mix(litScene, litScene + fogContribution, fogAlpha);

    // Add some atmospheric perspective
    result = mix(result, fogColor, fogAlpha * 0.3);

    finalColor = vec4(result, 1.0);
}";

        return Raylib.LoadShaderFromMemory(vertexShader, fragmentShader);
    }
}