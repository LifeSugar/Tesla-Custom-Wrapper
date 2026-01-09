# PlayMode 运行时 Gizmo 系统使用说明

## 功能概述
在PlayMode中提供类似Unity Editor的移动、旋转、缩放工具（WER），让你可以实时调整TestPoint贴纸的位置和大小。

## 快速设置步骤

### 1. 添加运行时Gizmo控制器
在场景中创建一个新的GameObject：
1. 右键 Hierarchy → Create Empty
2. 命名为 "RuntimeGizmoManager"
3. 添加脚本：`RuntimeGizmoControllerAdvanced`

### 2. 配置控制器
在Inspector中设置：
- **Target Camera**: 拖入Main Camera
- **Gizmo Size**: 调整Gizmo大小（推荐0.5-1.0）
- **Selection Radius**: 点击检测范围（推荐0.15）

### 3. 给TestPoint添加Collider
为了能够点击选择TestPoint，需要添加碰撞体：
1. 选中TestPoint GameObject
2. Add Component → Box Collider
3. 调整Collider大小以匹配贴纸

### 4. 启用运行时Gizmos
在TestPoint的Inspector中：
- 勾选 `Enable Runtime Gizmos`

## 使用方法

### 操作模式切换
- **W键**: 移动模式（Move）- 显示红绿蓝三个移动轴
- **E键**: 旋转模式（Rotate）- 显示三个旋转圆环
- **R键**: 缩放模式（Scale）- 显示带方块的缩放轴
- **ESC键**: 取消选择

### 操作流程
1. **进入PlayMode**
2. **点击TestPoint**选择物体
3. **按W/E/R键**切换操作模式
4. **点击并拖拽Gizmo的轴**进行变换
   - 移动模式：拖拽红/绿/蓝轴沿X/Y/Z方向移动
   - 旋转模式：拖拽对应颜色的圆环旋转
   - 缩放模式：拖拽对应轴进行缩放

### 颜色说明
- **红色**: X轴（左右）
- **绿色**: Y轴（上下）
- **蓝色**: Z轴（前后）

## 进阶功能（可选扩展）

### 方案1: 简化版（当前实现）
使用`RuntimeGizmoControllerAdvanced`提供基础的轴向拖拽功能。

### 方案2: 使用开源库（推荐）
如果需要更专业的效果，可以使用：
1. **Runtime Transform Gizmos** (免费)
   - GitHub: https://github.com/HiddenMonk/Unity3DRuntimeTransformGizmo
   - 提供完整的Editor风格Gizmo

2. **Runtime Transform Handles** (免费)
   - GitHub: https://github.com/Syy9/RuntimeHandles
   - URP/HDRP支持

### 安装开源库步骤
1. 下载源码到 `Assets/Plugins/RuntimeGizmos/`
2. 添加对应的Manager组件到场景
3. 在TestPoint上添加可选择标记

## 注意事项
1. TestPoint必须有Collider才能被选中
2. Gizmo大小会影响点击精度，建议根据场景调整
3. 在PlayMode中的修改不会保存，需要在EditMode中固化
4. 可以同时选择多个TestPoint（需要扩展脚本）

## 常见问题

**Q: 无法点击选择TestPoint？**
A: 检查是否添加了Collider组件

**Q: Gizmo不显示？**
A: 确保TestPoint的`enableRuntimeGizmos`已勾选，且已进入PlayMode

**Q: 拖拽不准确？**
A: 调整`selectionRadius`参数，增大选择容差

**Q: 想要保存PlayMode中的修改？**
A: 可以在脚本中添加序列化功能，或者使用EditorPrefs保存数据

## 代码文件说明
- `RuntimeGizmoControllerAdvanced.cs`: 主控制器，处理输入和变换逻辑
- `RuntimeGizmoRenderer.cs`: GL渲染器（可选，用于更复杂的视觉效果）
- `TestPointGizmo.cs`: 已修改，添加运行时Gizmo支持

## 扩展建议
1. 添加撤销/重做功能
2. 支持多选和批量操作
3. 添加吸附功能（网格吸附）
4. 保存/加载预设
5. 添加数值输入面板
