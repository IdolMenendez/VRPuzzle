# Blender 完整语义部件拆件与 Unity 精确重组流程 V01

日期：2026-08-28  
适用项目：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR`  
目标：把一份已经生成好的完整 3D 模型，在 Blender 中沿真实结构边界拆成若干“自身完整、可单独拿取”的语义部件，并让 Unity 能无人工摆放地精确恢复成原完整模型。

> 重要说明：`GardenCottage V01` 的最终实验路线没有使用 Blender，也没有裁切完整网格；它使用九个独立图生 3D 部件，再由 Unity 求解 `T × R × S`。本文记录的是仓库中另一条已经实际使用过的旧路线：**完整模型 → Blender 拓扑修复与语义拆件 → 保存共享坐标/解算矩阵 → Unity 精确重组**。两条路线不要混为一谈。

## 1. 核心结论

精确重组并不是拆完后再靠视觉把零件摆回去。可靠做法是：

1. 所有部件都从同一份完整母网格复制出来。
2. 拆件期间部件始终留在母模型坐标系内，不进行任意移动、旋转或独立缩放。
3. 裁切只删除不属于当前部件的面；保留下来的外观顶点、UV 和材质不动。
4. 新出现的切口单独封口，并使用独立的“接合面材质”。
5. 在任何爆炸展示、原点居中或单件导出之前，保存每个部件的完整变换矩阵。
6. Unity 使用保存的变换恢复部件，或直接读取包含全部部件的组合 FBX。这样各部件天然回到原位。

变换约定为：

```text
p_assembly = T × R × S × p_part
```

其中 `p_part` 是部件局部顶点，`S` 是缩放，`R` 是旋转，`T` 是平移。对于保持共享母模型原点的部件，通常 `T = 0`、`R = identity`、`S = 1`；如果为了抓取体验把每个部件原点改到几何中心，则必须保存原点变化后的 `T/R/S`。

## 2. 推荐的目录与版本结构

### 2.1 当前方案到底有多通用

当前实现是**通用执行框架 + 每模型语义配置**，不是完全无配置的一键拆件器，也不是人工逐面拆解。

通用执行框架负责：

- 导入与坐标冻结。
- 拓扑统计和有界局部修复。
- 根据分类结果复制母网格、删除非目标面并提取所有 Part。
- 连通分量分析、孤立碎片清理、切口封闭和接合面材质。
- 保存 solved `T/R/S` 和 4×4 矩阵。
- 组合/单件 FBX 导出、重新导入、拓扑与多角度审计。

每模型必须提供的配置负责：

- Part 数量、名称和语义。
- 建筑结构缝、空间区间或角度扇区。
- 哪些面/连通岛属于哪个 Part。
- 最小体积、完整性、允许的安装方向和支撑关系。
- 个别误归类连通岛的重新分配。
- 是否需要视觉化榫键、接口或独立接合面材料。

因此 Python 脚本不是逐个手工选择三角面，而是把模型专用的分类规则编码后，一次运行自动生成全部 Part。宝塔、风车、船和埃菲尔铁塔复用同一套管线骨架，但分类器不同：

- 宝塔：高度区间 + Loose Island 重分配。
- 风车：高度区间 + 对角线距离 + 径向连通分量。
- 船：面中心、法线、高度、左右舷和艏艉区域。
- 埃菲尔铁塔：归一化高度 + 绕中心轴角度扇区。

要升级为“任意新模型无需配置”的真正通用策略，还需要额外的语义分割/结构理解层：从多视角图像和完整网格提出 Part 候选，再以流形性、紧致度、最小厚度、结构缝显著性、可安装方向、支撑图和外观完整性作为联合目标优化。当前仓库尚未实现这一层，所以不能声称对任意模型零配置全自动。

每次拆件都建立新版本，不覆盖母模型和已通过版本：

```text
Artifacts/AssetCandidates/<Model>/<Date>_<Version>/
├── Source/
│   └── Hero_Source_Embedded.fbx
├── RepairedMaster/
│   ├── Hero_RepairedMaster.fbx
│   └── TopologyRepair.json
├── CutMaster/
│   ├── Hero_SemanticParts_Combined.fbx
│   ├── Hero_SemanticParts.blend
│   └── AssemblyMatrices.json
├── Individual/
│   ├── Part_01_*.fbx
│   └── Part_NN_*.fbx
└── Audit/
    ├── Assembled.png
    ├── Exploded.png
    ├── Part_01_Front.png
    ├── Part_01_Back.png
    ├── Part_01_Left.png
    ├── Part_01_Right.png
    ├── Part_01_Top.png
    ├── Part_01_Bottom.png
    ├── Topology.json
    └── ASSET_GATE.md
```

`Source` 永远只读。拓扑修复、拆件、视觉修正分别产生新版本，不能在同一个 FBX 上反复覆盖。

## 3. 阶段 A：完整模型导入与坐标冻结

### 3.1 Blender 手动操作

1. 打开 Blender，选择 `File > Import > FBX (.fbx)` 或 `glTF 2.0 (.glb/.gltf)`。
2. 删除导入模型自带的 Camera、Light、动画控制器，只保留英雄模型及其材质。
3. 在 Object Mode 选择英雄模型，按 `N` 打开 Transform 面板，记录：
   - Location
   - Rotation
   - Scale
   - Dimensions
4. 执行 `Ctrl+A > Rotation & Scale`。
5. **不要执行 `Apply Location`，不要执行 `Set Origin > Origin to Geometry`，不要把模型移动到世界原点。**
6. 复制一份只读母体：`Shift+D` 后立即右键取消移动，命名为 `Hero_Source_Locked`。
7. 再复制一份工作体，命名为 `Hero_RepairedMaster_Working`。

应用 Rotation/Scale 的原因是让后续切割平面工作在稳定的对象坐标中；保留 Location 和共享原点，是为了让每个拆件自然携带同一装配坐标系。

### 3.2 自动脚本对应操作

仓库的实际脚本采用：

```python
bpy.ops.import_scene.fbx(filepath=source_path)
source = visible_meshes[0]
bpy.context.view_layer.objects.active = source
source.select_set(True)
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
```

关键参数是 `location=False`。这保证不会因“看起来方便”而破坏原模型的装配基准。

## 4. 阶段 B：拆件前拓扑体检与局部修复

AI 生成模型常见问题包括：开放边界、多面共边、线边、孤立点、重叠顶点、退化三角形和错误法线。如果直接切，会导致封口失败或出现巨大的错误面。

### 4.1 必查指标

对母模型统计：

```text
boundary_edges      边界边，理想值 0
wire_edges          没有面的线边，必须 0
multi_face_edges    被三个以上面共享的边，必须 0
non_manifold_edges  非流形边，必须 0
loose_vertices      不连接任何边的点，必须 0
```

手动检查：

1. 进入 Edit Mode。
2. `A` 全选。
3. `M > By Distance`，只合并极小距离内的重复点。
4. `Mesh > Clean Up > Degenerate Dissolve`。
5. `Select > Select All by Trait > Non-Manifold` 检查开放边和多面共边。
6. `Shift+N` 重新计算外侧法线。

### 4.2 修复原则

- 优先局部修复，不要对整模型做 Voxel Remesh。
- 不使用全局 Solidify 修复开放网格；它会改变外轮廓和 UV。
- 外观表面原顶点、原 UV、原材质索引尽量保持不动。
- 多面共边只删除明显多余的内部/重叠面。
- 小闭合边界可 Fill；巨大、分叉或自交边界必须回到上一步重新分类，不能强行封口。

仓库中的可复用修复实现：

`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Tools/AI3D/repair_v09_pagoda_master.py`

该脚本执行：

1. Merge by Distance。
2. Dissolve Degenerate。
3. 在多面共边上保留绕向相反、法线连续性最高、面积更可信的一对表面。
4. 删除多余面。
5. 对闭合小边界执行 `holes_fill` 或有界 fan fill。
6. 从邻接面插值新封口的 UV。
7. Recalculate Face Normals。
8. 导出后重新统计拓扑，并比较修复前后的 Bounds。

## 5. 阶段 C：语义拆件规划

拆件不是平均切片。每一个 Part 必须像一个真实模型组件，而不是从模型上撕下来的一块皮。

### 5.1 先写 Part Contract

拆之前先写表格：

| 字段 | 含义 |
|---|---|
| Part 名称 | `Part_01_Foundation` 等稳定名称 |
| 语义 | 地基、完整墙体、完整屋顶、整棵树等 |
| 边界 | 建筑檐口、楼层线、榫槽、甲板线等真实结构边界 |
| 支撑面 | 与前置部件接触的完整区域 |
| 安装方向 | 从上、前、侧面或斜向接近 |
| 最小厚度 | 防止生成薄片或不可抓取碎片 |
| 允许子岛 | 同一部件可包含哪些物理连接的子结构 |
| 禁止内容 | 不得混入的邻近结构 |

### 5.2 选择拆件算法

按结构选择三种方式：

#### 方法一：按 Loose Island 分离

适用于模型本来就由不相连网格岛组成。

手动操作：

1. Edit Mode，`P > By Loose Parts`。
2. 回到 Object Mode。
3. 逐个部件检查连通岛，不按数量直接认定语义。
4. 选中属于同一语义部件的岛，最后选择主对象，`Ctrl+J` 合并。

脚本对应 `bpy.ops.mesh.separate(type="LOOSE")`。仓库宝塔脚本使用该方法把完整树木岛从楼层模块重新分配到景观模块。

#### 方法二：按结构平面 Bisect

适用于塔、楼层、地基、屋顶等具有稳定结构层级的模型。

手动操作：

1. 从锁定母体复制一份：`Shift+D`，右键取消位移。
2. 进入 Edit Mode，`A` 全选。
3. 选择 `Mesh > Bisect`，在正交视图沿真实结构缝切第一刀。
4. 在操作面板启用 `Clear Inner` 或 `Clear Outer`，只保留目标区间一侧。
5. 在另一结构缝切第二刀，保留两平面之间的完整区段。
6. 不要凭屏幕像素估算切面；在 `N` 面板或脚本里使用明确的局部坐标数值。

脚本核心：

```python
duplicate = source.copy()
duplicate.data = source.data.copy()
bpy.context.collection.objects.link(duplicate)

bm = bmesh.new()
bm.from_mesh(duplicate.data)

bmesh.ops.bisect_plane(
    bm,
    geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
    plane_co=(0.0, 0.0, minimum_z),
    plane_no=(0.0, 0.0, 1.0),
    clear_inner=True,
    clear_outer=False,
    dist=1.0e-5,
)

bmesh.ops.bisect_plane(
    bm,
    geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
    plane_co=(0.0, 0.0, maximum_z),
    plane_no=(0.0, 0.0, 1.0),
    clear_inner=False,
    clear_outer=True,
    dist=1.0e-5,
)
```

这里不是把一个对象切九次后移动，而是对同一母体复制九份，每份只保留自己的语义区间。所有复制品仍在母模型坐标中，因此组合时天然重合。

#### 方法三：按空间区域和面语义分类

适用于船体左右舷、埃菲尔铁塔四个方向、风车对角叶片等不能用水平面表达的结构。

每个面根据下列信息分类：

- 面中心的 `x/y/z`。
- 高度归一化值。
- 面法线方向。
- 绕中心轴的角度扇区。
- 距离某条结构轴线或对角线的距离。
- 所属连通分量的面积、跨度和主方向。

示例：

```python
center_world = source.matrix_world @ polygon.center
normal_world = source.matrix_world.to_3x3() @ polygon.normal

if center_world.z < keel_height:
    part = "Keel"
elif center_world.y < 0:
    part = "PortHull"
else:
    part = "StarboardHull"
```

分类必须满足：每个原始面恰好属于一个 Part，既不能遗漏，也不能重复。

仓库实例：

- 船体区域分类：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Tools/Stage5/build_meshy_ship_semantic10_v03.py`
- 埃菲尔铁塔高度与角度扇区分类：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Tools/Stage7/build_meshy_eiffel_semantic16_v04.py`
- 风车水平结构与对角叶片分类：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Tools/AI3D/build_v09_windmill_cut_master.py`
- 宝塔结构层级切割：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Tools/AI3D/build_v09_pagoda_cut_master.py`

## 6. 阶段 D：切口封闭与接合面处理

删除非目标面后，部件通常会出现边界环。每个 Part 必须成为有体积感的完整组件。

### 6.1 手动封口

1. Edit Mode，选择切口边界环。
2. 简单平面边界按 `F` 填面。
3. 规则四边结构使用 `Ctrl+F > Grid Fill`。
4. `Shift+N` 重新计算法线。
5. 对新面执行 `Shade Flat`，避免切口出现鼓包。
6. 新建 `MatingCap` 材质，仅把新封口面 Assign 到该材质槽。

### 6.2 脚本封口

```python
boundary = [edge for edge in bm.edges if edge.is_boundary]
result = bmesh.ops.holes_fill(bm, edges=boundary, sides=0)
for face in result.get("faces", ()):
    face.material_index = cap_material_index
    face.smooth = False
```

### 6.3 材质原则

- 外观表面保留原 UV 和 PBR 贴图。
- 新封口没有合理的原图 UV，必须使用单独的低反差接合面材质。
- 接合面颜色应随模块语义选择，例如木材、石材、深色屋顶或黄铜。
- 不要把原外墙纹理强行拉伸到切口上。
- 接合面在组装态应被邻接部件遮住，在爆炸态应像经过处理的模型接口。

## 7. 阶段 E：坐标、原点和矩阵保存

这是精确拼接最关键的阶段。

### 7.1 方案 A：共享母模型原点

适合组合 FBX 或不要求每个零件抓取原点位于自身中心的情况。

规则：

- 不执行 `Origin to Geometry`。
- 不移动任何 Part。
- 所有 Part 继承母模型相同的 Object Transform。
- 组合导出后，Unity 把所有子对象放在同一个父节点下即可原样恢复。

优点：误差最小，装配矩阵通常是 Identity。  
缺点：某些 Part 的局部原点可能离自身几何很远，不利于单独旋转或抓取。

### 7.2 方案 B：部件原点居中，但冻结装配矩阵

适合每个 Part 都需要稳定旋转中心的交互模型。

操作顺序：

1. 部件仍在正确装配位置时，记录世界矩阵：

```python
solved_matrix = part.matrix_world.copy()
```

2. 执行 `Object > Set Origin > Origin to Geometry`。
3. 再次记录新的 `location/rotation/scale`，这就是把该居中部件恢复到母模型时需要的 solved pose。
4. 导出单件时可以临时把 `location` 设为零；导出完成后立即恢复。
5. 把 solved pose 和 4×4 矩阵写入 JSON，不能只写包围盒中心。

建议 JSON：

```json
{
  "part": "Part_03_BlueRoofChimney",
  "sourceAsset": ".../Part_03.fbx",
  "position": [0.0, 1.825, 0.18],
  "rotationQuaternion": [0.0, 0.0, 0.0, 1.0],
  "scale": [1.0, 1.0, 1.0],
  "localToAssemblyMatrixRowMajor": [
    1, 0, 0, 0,
    0, 1, 0, 1.825,
    0, 0, 1, 0.18,
    0, 0, 0, 1
  ]
}
```

### 7.3 不允许的操作

- 拆完后逐件 `Alt+G / Alt+R / Alt+S` 再凭视觉摆回去。
- 导出前给每个 Part 随意 Apply Location。
- 单独导出时由工具自动居中，却没有保存原装配变换。
- 使用 Bounds Center 代替真实结构锚点和朝向。
- 一个部件使用非等比缩放去补偿错误的切割或错误语义。

## 8. 阶段 F：FBX 导出

### 8.1 组合 FBX

组合 FBX 中保留所有 Part 的装配位置，是最可靠的权威版本。

推荐设置：

```python
bpy.ops.export_scene.fbx(
    filepath=output_fbx,
    use_selection=True,
    apply_unit_scale=True,
    bake_space_transform=False,
    object_types={"MESH", "EMPTY"},
    add_leaf_bones=False,
    path_mode="COPY",
    embed_textures=True,
)
```

`bake_space_transform=False` 很关键：所有部件都使用同一导出坐标约定，避免每个 Part 被独立轴向烘焙。

### 8.2 单件 FBX

每个 Part 可另存一份，供 Unity 抓取与碰撞使用，但组合 FBX 和矩阵 JSON 仍是装配真值。

导出前：

1. 保存 `original_location/rotation/scale`。
2. 如果单件资产需要局部原点为零，只临时把 Object Location 设为零。
3. 导出后恢复 Transform。
4. 不改变 Mesh Data 中的顶点坐标。

### 8.3 Blender 命令行批处理

本机 Blender：

`/Applications/Blender.app/Contents/MacOS/Blender`

宝塔拆件实例：

```bash
"/Applications/Blender.app/Contents/MacOS/Blender" \
  --background \
  --python "/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Tools/AI3D/build_v09_pagoda_cut_master.py" \
  -- \
  "/absolute/path/to/RepairedMaster.fbx" \
  "/absolute/path/to/SemanticParts_Combined.fbx" \
  "/absolute/path/to/Audit"
```

先修复再拆件时，先运行：

```bash
"/Applications/Blender.app/Contents/MacOS/Blender" \
  --background \
  --python "/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Tools/AI3D/repair_v09_pagoda_master.py" \
  -- \
  "/absolute/path/to/Source.fbx" \
  "/absolute/path/to/RepairedMaster.fbx" \
  "/absolute/path/to/RepairAudit"
```

## 9. 阶段 G：重新导入审计

不能只审查 Blender 内存中的对象。必须把导出的 FBX 在一个干净场景中重新导入，因为轴向、单位、材质和层级问题常在导出后才出现。

### 9.1 自动拓扑门槛

每个 Part 必须满足：

```text
boundary_edges = 0
wire_edges = 0
multi_face_edges = 0
non_manifold_edges = 0
loose_vertices = 0
polygons > 0
bounds 全部有限
```

仓库审计脚本：

- `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Tools/AI3D/audit_v09_fbx_objects.py`
- `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Tools/AI3D/audit_v09_topology_defects.py`

### 9.2 几何重组误差

至少验证：

1. 组合 Part 数量等于合同数量。
2. 全部 Part 的面集合完整覆盖母模型外观面。
3. 组合后的整体 Bounds 与母模型 Bounds 一致。
4. 未修改外观顶点时，重组后对应顶点世界坐标误差建议 `< 1e-5`。
5. 所有封口面在组装态均被正确遮挡。
6. 没有重复外观面引起闪烁，也没有缺失面形成透光缝。

### 9.3 六面视觉审计

每个 Part 单独渲染：

- Front
- Back
- Left
- Right
- Top
- Bottom

同时渲染：

- Assembled
- Exploded
- Assembled 与母模型 Overlay/Difference

即使拓扑统计为 0，也必须拒绝以下视觉结果：

- 像纸片或撕裂碎块。
- 屋顶、墙面、船板只有正面没有背面。
- 一个 Part 混入邻近模型的悬浮碎片。
- 封口面盖住正常外观。
- 切口穿过装饰主体，而不是沿结构缝。
- 爆炸态不可理解该部件是什么。

仓库中通过拓扑门槛的宝塔审计实例：

`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/AssetAudit/Pagoda/20260721_V09_RepairedCutMaster9_CandidateL/PAGODA_V09_ASSET_GATE.md`

## 10. 阶段 H：Unity 精确重组

### 10.1 组合 FBX 方式

1. 将组合 FBX 导入 Unity。
2. 实例化其根节点。
3. 保持根节点 `localPosition = 0`、`localRotation = identity`、`localScale = 1`。
4. FBX 内各 Part 子节点已经携带正确相对变换，因此会直接恢复完整模型。

### 10.2 单件 FBX + 矩阵方式

1. 分别实例化 Part FBX。
2. 从 JSON 读取 `position`、`rotationQuaternion`、`scale`。
3. 应用：

```csharp
part.localPosition = position;
part.localRotation = rotation;
part.localScale = scale;
Matrix4x4 solved = Matrix4x4.TRS(position, rotation, scale);
```

4. Unity 使用列向量约定：

```text
p_assembly = T × R × S × p_part
```

5. 不要把矩阵顺序写成 `S × R × T`。
6. Blender 是 Z-up，Unity 是 Y-up。不要手工逐 Part 调轴；应让所有资产走同一套 FBX 导出/Unity ModelImporter 轴向转换，并在 Unity 导入后记录最终矩阵。

### 10.3 Unity 侧最终门槛

- 组合态与 Blender Assembled 六面图一致。
- 每个 Part 的 MeshFilter/MeshRenderer 均指向预期资产。
- 所有 Part 使用统一根尺度，不混用 `100`、`1`、`0.01`。
- 组装态接缝不漏光、不闪烁。
- 场景保存后重新打开，矩阵不变化。
- 场景与备份文件 SHA-256 一致。

## 11. 常见失败及处理

### 11.1 `holes_fill` 生成巨大斜面

原因：边界不是单一闭合环，或模型仍有多面共边。  
处理：撤销封口；先修复拓扑；按连通分量逐环检查；对巨大边界重新规划切割面。

### 11.2 部件拼回去仍有缝

原因通常不是 Unity 浮点误差，而是：

- 切割两侧使用了不同平面参数。
- 某一部件单独 Apply Location。
- 单件导出被自动居中但没有保存 solved matrix。
- 导出时混用了不同轴向或单位设置。
- 使用 Bounds Center 重新估算位置，而不是共享坐标/已冻结矩阵。

### 11.3 拼回去重叠闪烁

原因：两个 Part 都保留了同一批外观面。  
处理：执行面分区完整性检查，确保每个原始面只属于一个 Part；接合面只能处在内部。

### 11.4 部件像碎片

原因：仅按高度或面积机械分段，没有语义合同。  
处理：沿真实结构缝重做；把完整装饰岛、完整树、完整屋面、完整门窗组归到一个可理解部件；必要时减少 Part 数量。

### 11.5 纹理在切口处拉花

原因：把原外观 UV 投射给新封口。  
处理：封口使用独立材质；外观 UV 不动；不对整个 Part 重新展开 UV。

## 12. 推荐执行顺序清单

```text
[ ] 冻结只读 Source FBX 与哈希
[ ] 导入并 Apply Rotation/Scale，不 Apply Location
[ ] 保存共享母模型坐标和 Bounds
[ ] 拓扑审计与有界局部修复
[ ] 写 Part Contract 和结构边界
[ ] 从同一母体原位复制每个 Part
[ ] 按 Loose Island / Bisect / 空间语义分类拆件
[ ] 检查面分区无遗漏、无重复
[ ] 清理孤立点、线边和微小错误岛
[ ] 对切口逐环封闭并赋接合面材质
[ ] 保存每个 Part 的 solved T/R/S 与 4×4 矩阵
[ ] 导出组合 FBX
[ ] 导出独立 Part FBX
[ ] 在干净 Blender 场景重新导入
[ ] 通过 topology、bounds、matrix、六面视觉门槛
[ ] Unity 导入并按冻结矩阵重组
[ ] 保存独立 Unity 场景和备份
[ ] 输出正面、多角度、爆炸态、Overlay/Difference 截图
[ ] 写 VALIDATION.md 与 SHA-256 清单
```

## 13. 本仓库可复用文件索引

- 母模型局部拓扑修复：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Tools/AI3D/repair_v09_pagoda_master.py`
- 水平结构层语义拆件：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Tools/AI3D/build_v09_pagoda_cut_master.py`
- 对角叶片与结构区域拆件：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Tools/AI3D/build_v09_windmill_cut_master.py`
- 船体面中心/法线/区域分类：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Tools/Stage5/build_meshy_ship_semantic10_v03.py`
- 埃菲尔铁塔高度/角度扇区分类：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Tools/Stage7/build_meshy_eiffel_semantic16_v04.py`
- FBX 对象审计：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Tools/AI3D/audit_v09_fbx_objects.py`
- 拓扑缺陷审计：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Tools/AI3D/audit_v09_topology_defects.py`
- 宝塔已通过拓扑审计示例：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/AssetAudit/Pagoda/20260721_V09_RepairedCutMaster9_CandidateL/PAGODA_V09_ASSET_GATE.md`
- 无 Blender 独立部件路线对照：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Docs/GARDEN_COTTAGE_NO_BLENDER_IMAGE_TO_PARTS_ASSEMBLY_V01.md`

## 14. 最终判断

Blender 拆件路线能做到数学意义上的精确回装，前提不是“算法重新猜测每个零件的位置”，而是**拆件从一开始就不丢失母模型坐标**。最稳方案是保留一份组合 FBX 作为装配真值，同时为交互导出原点居中的单件 FBX，并用 JSON 冻结每个部件的 `T × R × S`。Unity 只消费这套已经验证的矩阵，不再从截图或 Bounds 临时猜位置。
