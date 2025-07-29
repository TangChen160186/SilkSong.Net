# Radiance Cascades 2D 实现

这是一个基于raylib的Radiance Cascades 2D全局光照算法实现。

## 什么是Radiance Cascades？

Radiance Cascades是一种新颖的全局光照算法，由Alexander Sannikov开发。它基于两个关键观察：

### 核心原理

1. **角度观察（Angular Observation）**：
   - 距离光源越远，需要更高的角度分辨率（更多光线方向）
   - 距离光源越近，需要更高的空间分辨率（更密集的探针）

2. **空间观察（Spatial Observation）**：
   - 角度分辨率和空间分辨率成反比关系
   - 这被称为"半影条件"（Penumbra Condition）

### 级联结构

算法使用多个"级联"（Cascades），每个级联都是一个探针网格：

- **Cascade 0**: 最多探针，最少光线方向（高空间分辨率，低角度分辨率）
- **Cascade 1**: 1/4探针，4倍光线方向  
- **Cascade 2**: 1/16探针，16倍光线方向
- 以此类推...

### 工作流程

1. **光线投射阶段**：
   - 每个级联的每个探针向不同方向投射光线
   - 使用SDF（Signed Distance Field）进行场景表示
   - 计算光线与场景的交互，包括阴影和光照

2. **级联合并阶段**：
   - 从最高级联开始，向下合并到Cascade 0
   - 使用双线性插值处理不同分辨率之间的数据
   - 考虑光线可见性，处理遮挡关系

3. **最终渲染阶段**：
   - 从Cascade 0提取全局光照信息
   - 对屏幕上每个像素进行光照计算
   - 显示最终的全局光照效果

## 实现特点

### 数据结构

```csharp
// 辐射探针 - 存储位置和各方向的辐射度
public struct RadianceProbe
{
    public Vector2 Position;
    public Vector4[] Radiance; // RGB + 可见性
    public int DirectionCount;
}

// 级联 - 探针网格
public struct RadianceCascade  
{
    public RadianceProbe[] Probes;
    public int ProbeCountX, ProbeCountY;
    public float ProbeSpacing;
    public float IntervalStart, IntervalLength;
    public int DirectionsPerProbe;
}
```

### SDF场景表示

使用简单的SDF函数定义场景几何体：
- 圆形：`distance(point, center) - radius`
- 矩形：基于距离的盒子SDF
- 场景组合：取最小距离（并集操作）

### 光线投射优化

- 使用SDF加速的光线步进
- 自适应步长：根据SDF距离调整步进大小
- 早期终止：光线离开屏幕或击中表面时停止

## 控制说明

- **鼠标左键**：移动光源位置
- **SPACE**：切换显示探针位置
- **R**：切换显示全局光照效果
- **C**：循环查看不同级联
- **上/下箭头**：调整光照强度
- **1-5数字键**：改变光源颜色（红、绿、蓝、黄、白）

## 技术细节

### 级联参数计算

```csharp
// 计算需要的级联数量
float diagonal = sqrt(sceneWidth² + sceneHeight²);
cascadeCount = ceil(log₄(diagonal / baseInterval)) + 1;

// 每个级联的间隔参数
intervalScale = 4^cascadeIndex;
intervalStart = baseInterval * (1 - intervalScale) / (1 - 4);
intervalLength = baseInterval * intervalScale;
```

### 合并算法

1. **角度合并**：将4个高级联光线合并为1个低级联光线
2. **空间合并**：使用双线性插值处理4个最近探针
3. **可见性处理**：近距离间隔可以遮挡远距离间隔

### 性能优化

- 降低渲染分辨率（每4像素采样一次）
- 限制级联数量（最多6个）
- SDF加速的光线投射
- 早期光线终止

## 已知限制

1. **锐利阴影**：RC对非常锐利的阴影表现不佳
2. **光线泄漏**：某些情况下可能出现光线泄漏
3. **性能**：实时计算仍然较为昂贵
4. **简化实现**：这是教学版本，缺少一些高级优化

## 扩展可能性

1. **3D扩展**：算法可以扩展到3D空间
2. **体积光**：支持体积散射效果
3. **天空盒积分**：集成环境光照
4. **时间滤波**：减少噪声和闪烁
5. **GPU实现**：使用计算着色器加速

## 参考资料

- [Alexander Sannikov的原始论文](https://github.com/Raikiri/RadianceCascadesPaper)
- [GM Shaders教程](https://mini.gmshaders.com/p/radiance-cascades)
- [MΛX的基础教程](https://m4xc.dev/articles/fundamental-rc/)
- [Fad的Shadertoy实现](https://www.shadertoy.com/view/mtlBzX)

这个实现展示了Radiance Cascades的核心概念，虽然简化但包含了算法的主要组成部分。
