#version 330

in vec2 fragTexCoord;
out vec4 fragColor;

uniform sampler2D texture0;
uniform float heightScale = 0.1; // 高度缩放因子

void main() {
    // 计算纹理像素大小
    vec2 texelSize = 1.0 / textureSize(texture0, 0);
    
    // 采样相邻像素的亮度（灰度值）
    float center = texture(texture0, fragTexCoord).r;
    float right = texture(texture0, fragTexCoord + vec2(texelSize.x, 0)).r;
    float left = texture(texture0, fragTexCoord - vec2(texelSize.x, 0)).r;
    float up = texture(texture0, fragTexCoord + vec2(0, texelSize.y)).r;
    float down = texture(texture0, fragTexCoord - vec2(0, texelSize.y)).r;
    
    // 使用Sobel算子计算梯度
    float dx = (right - left) * heightScale;
    float dy = (up - down) * heightScale;
    
    // 计算法线向量 (切线空间)
    vec3 normal = normalize(vec3(-dx, -dy, 1.0));
    
    // 将法线从[-1,1]映射到[0,1]用于RGB显示
    normal = normal * 0.5 + 0.5;
    
    // 输出法线贴图
    fragColor = vec4(normal, 1.0);
}