# Tesla 贴纸系统 - 使用指南

## 📦 系统组件

### 1. 核心脚本
- **DecalData.cs** - 贴纸数据结构
- **DecalManager.cs** - 贴纸管理器（核心组件）
- **DecalSystemTest.cs** - 测试脚本

### 2. Shader
- **DecalProjection.shader** - 贴纸投影shader
- **PositionBaker.shader** - 位置图烘焙shader（已有）
- **NormalBaker.shader** - 法线图烘焙shader（已有）

---

## 🚀 快速开始

### 步骤1: 烘焙数据图

1. 打开工具：`Tools → Tesla Painto → Baker (Multi-Mesh)`
2. 拖入车辆根节点
3. 选择 `Bake Type: Both`
4. 点击 `烘焙位置和法线图 (Bake Both Maps)`
5. 生成的EXR文件位于：`Assets/Textures/BakedMaps/`

### 步骤2: 设置场景

1. **创建GameObject**
   - 在场景中创建空GameObject，命名为 `DecalSystem`
   - 添加 `DecalManager` 组件

2. **配置DecalManager**
   - Position Map: 拖入烘焙的位置图EXR
   - Normal Map: 拖入烘焙的法线图EXR
   - Target Material: 拖入车辆材质
   - Decal Layer Property Name: `_DecalLayer` (材质中接收贴纸层的属性名)
   - Resolution: 2048

3. **添加测试脚本（可选）**
   - 创建另一个GameObject，命名为 `DecalTester`
   - 添加 `DecalSystemTest` 组件
   - Decal Manager: 拖入刚才创建的DecalSystem对象
   - Test Decal Texture 1/2/3: 拖入你的测试贴纸图片

### 步骤3: 修改车辆Shader

在你的车辆shader中添加贴纸层支持：

```shader
Properties
{
    // ... 其他属性
    _DecalLayer ("Decal Layer", 2D) = "black" {}
}

// 在fragment shader中
sampler2D _DecalLayer;

float4 frag(v2f i) : SV_Target
{
    // 获取基础颜色
    float4 baseColor = tex2D(_MainTex, i.uv);
    
    // 采样贴纸层
    float4 decalColor = tex2D(_DecalLayer, i.uv);
    
    // Alpha混合
    float3 finalColor = lerp(baseColor.rgb, decalColor.rgb, decalColor.a);
    
    return float4(finalColor, 1.0);
}
```

---

## 🎮 测试功能

运行游戏后，可以使用以下按键测试：

- **1/2/3** - 添加预设的测试贴纸
- **R** - 添加随机贴纸
- **C** - 清空所有贴纸
- **鼠标左键** - 在点击的3D位置添加贴纸（需要有Collider）

---

## 🎨 通过代码添加贴纸

```csharp
// 获取DecalManager
DecalManager manager = FindObjectOfType<DecalManager>();

// 创建贴纸数据
DecalData newDecal = new DecalData
{
    decalName = "我的贴纸",
    decalTexture = myTexture,           // 你的贴纸图片
    worldPosition = new Vector3(0, 1, 0), // 3D世界坐标
    projectionDirection = Vector3.down,   // 投影方向
    size = 0.2f,                         // 尺寸（米）
    rotation = 45f,                      // 旋转角度
    opacity = 1f,                        // 不透明度
    tintColor = Color.white,             // 着色
    blendMode = DecalData.BlendMode.AlphaBlend
};

// 添加到管理器
manager.AddDecal(newDecal);
```

---

## 📋 贴纸参数说明

### DecalData 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `decalTexture` | Texture2D | 贴纸图片（建议PNG带透明通道）|
| `worldPosition` | Vector3 | 贴纸在3D空间中的位置 |
| `projectionDirection` | Vector3 | 投影方向（通常是表面法线的反方向）|
| `size` | float | 贴纸大小（单位：米）|
| `rotation` | float | 贴纸旋转角度（度）|
| `opacity` | float | 不透明度 (0~1) |
| `tintColor` | Color | 着色/颜色调制 |
| `blendMode` | enum | 混合模式（AlphaBlend/Additive/Multiply）|
| `projectionDepth` | float | 投影深度，防止贴纸拉伸 |

---

## 🔧 高级功能

### 1. 动态更新贴纸
```csharp
// 修改贴纸参数后，调用更新
decal.rotation = 90f;
decal.opacity = 0.5f;
manager.UpdateDecals();
```

### 2. 移除特定贴纸
```csharp
manager.RemoveDecal(specificDecal);
```

### 3. 清空所有贴纸
```csharp
manager.ClearAllDecals();
```

### 4. 自定义混合模式

在 `DecalProjection.shader` 中修改混合模式：
- `AlphaBlend`: 标准透明混合
- `Additive`: 发光效果
- `Multiply`: 阴影/正片叠底效果

---

## ⚠️ 注意事项

1. **UV不重叠**: 所有车辆部件的UV必须不重叠（像官方模板那样）
2. **统一Pivot**: 所有部件必须共用同一个原点
3. **EXR格式**: Position和Normal Map必须是32位浮点EXR格式
4. **sRGB关闭**: 确保Position/Normal Map的Import Settings中 `sRGB` 已关闭
5. **分辨率匹配**: DecalManager的resolution应该与烘焙时的分辨率匹配

---

## 🐛 故障排除

### 问题1: 贴纸没有显示
- 检查Position/Normal Map是否正确加载
- 确认Target Material已分配
- 查看Console是否有shader错误

### 问题2: 贴纸位置错误
- 确认车辆的Transform是否正确（Scale = 1,1,1）
- 检查worldPosition是否在车辆范围内
- 尝试调整projectionDirection

### 问题3: 贴纸被拉伸
- 增大 `projectionDepth` 值
- 检查车辆模型是否有非均匀缩放

### 问题4: 性能问题
- 降低贴纸数量
- 减小RenderTexture分辨率
- 考虑将贴纸烘焙为静态纹理

---

## 📝 下一步扩展

- [ ] 可视化编辑器（Scene视图Gizmo）
- [ ] 撤销/重做系统
- [ ] 贴纸保存/加载功能
- [ ] 多层贴纸管理（Layer系统）
- [ ] 贴纸预览窗口
- [ ] 批量导入贴纸库

---

## 📧 技术支持

如有问题，请检查：
1. Unity Console的错误信息
2. Shader编译是否成功
3. 所有贴图的Import Settings

Happy Decal Painting! 🎨
