# 可交互Gizmo使用说明

## 功能概述
`InteractiveGizmo`提供可视化的实体Gizmo，支持直接拖拽边和顶点来调整贴纸大小。

## 快速设置

### 1. 添加组件
在已有TestPointGizmo的GameObject上：
- Add Component → Interactive Gizmo

组件会自动附加到TestPointGizmo上。

### 2. Inspector设置
**TestPointGizmo:**
- ✓ `Use Interactive Gizmo` - 启用可交互Gizmo
- ✓ `Show Gizmos` - 总开关

**InteractiveGizmo:**
- `Show Gizmo` - 显示/隐藏
- `Main Color` - 主平面颜色（半透明黄色）
- `Frame Color` - 边框颜色（绿色）
- `Handle Color` - 控制点颜色（红色）
- `Handle Size` - 控制点大小

## 使用方法

### 可视化元素
- **半透明平面** - 显示贴纸的覆盖区域
- **绿色边框** - 标识贴纸边界
- **红色球体** - 可拖拽的控制点
  - 边中点（4个）- 单轴缩放
  - 顶点（4个）- 等比缩放

### 交互操作

#### 单轴缩放（拖拽边中点）
1. 点击并拖拽**右侧/左侧**中点 → 改变**宽度**（X轴）
2. 点击并拖拽**顶部/底部**中点 → 改变**高度**（Y轴）

#### 等比缩放（拖拽顶点）
1. 点击并拖拽**任意顶点** → **宽度和高度**同时缩放
2. 向外拖拽放大，向内拖拽缩小

### 工作模式
- **Editor模式** - Gizmo始终可见和交互
- **PlayMode** - 需要确保`showGizmos`和`useInteractiveGizmo`都启用

## 技术细节

### 渲染方式
使用Unity Mesh + URP Unlit材质：
- 主平面：Quad mesh，透明渲染
- 边框：4个细长Cube
- 控制点：Sphere primitive

### 材质设置
- 透明混合模式
- 不投射阴影
- RenderQueue 3000-3002（确保在透明队列）

### 交互检测
- 使用Raycast检测控制点的SphereCollider
- 世界空间拖拽，自动转换到本地坐标
- 最小尺寸限制：0.01单位

## 与其他系统的关系

### TestPointGizmo
- 直接修改`decalWidth`和`decalHeight`
- 触发`SyncTransformToData()`更新贴纸显示
- 不依赖Transform.localScale

### RuntimeGizmoController
- 可与RuntimeGizmoController共存
- 通过`useInteractiveGizmo`开关切换
- InteractiveGizmo优先级更高（专门为贴纸设计）

### DecalManager
- 自动通过TestPointGizmo同步
- 实时更新贴纸渲染

## 性能注意事项
1. 每个TestPoint创建9个GameObject（1根+1平面+4边+4控制点）
2. 使用Unlit shader，开销较小
3. 仅在需要时显示（通过showGizmo控制）
4. 建议在大量TestPoint场景中使用LOD或选择性显示

## 自定义扩展

### 修改控制点形状
在`CreateHandle()`中更改`PrimitiveType`：
```csharp
GameObject.CreatePrimitive(PrimitiveType.Cube); // 改为立方体
```

### 添加深度控制
可扩展添加Z轴控制点来调整`projectionDepth`：
```csharp
// 在projectionDirection方向上创建控制点
Vector3 depthHandlePos = Vector3.forward * projectionDepth;
```

### 颜色高亮
在拖拽时改变控制点颜色：
```csharp
if (isDragging)
    handleMaterial.SetColor("_BaseColor", Color.yellow);
```

## 故障排查

**Q: Gizmo不显示？**
- 检查`showGizmo`和`testPoint.showGizmos`都已启用
- 确认Camera能看到Gizmo位置
- 检查URP Renderer设置是否支持透明渲染

**Q: 无法点击控制点？**
- 确认控制点有SphereCollider
- 检查Camera是否正确设置
- 使用Physics.Raycast调试射线

**Q: 拖拽时抖动？**
- 调整屏幕空间到世界空间的转换系数
- 降低`mouseDelta`的缩放因子（目前0.001f）

**Q: 颜色不对？**
- 确认使用URP项目
- 检查Shader是否为"Universal Render Pipeline/Unlit"
- 查看材质的_BaseColor属性

## 快捷键（可扩展）
未来可添加：
- `G` - 切换Gizmo显示
- `Shift+拖拽` - 吸附到网格
- `Ctrl+拖拽` - 精细调整
