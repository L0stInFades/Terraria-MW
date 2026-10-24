# 继续开发 Terraria: MW

仓库名是 `Terraria-MW`，tModLoader 内部模组名仍是 **ModernArsenal**。保留内部名可继续识别已有存档里的枪械、弹药和炮塔。tModLoader 原生构建还要求源码目录名与内部名相同，因此按下面的命令克隆到 `ModernArsenal`。

## Windows 环境与构建

安装并运行一次 Terraria 和 tModLoader。当前验证环境为 Terraria 1.4.4.9、tModLoader 2026.8.3、随 tModLoader 附带的 .NET 8；游戏和前置程序集不放进仓库。

```powershell
git clone https://github.com/L0stInFades/Terraria-MW.git ModernArsenal
cd ModernArsenal
$env:TML_PATH = 'E:\SteamLibrary\steamapps\common\tModLoader'
.\tools\build.ps1
```

把 `TML_PATH` 改成实际安装目录，里面应有 `tModLoader.dll`、`tMLMod.targets`、`dotnet/dotnet.exe` 和 `Libraries/`。构建工具会使用随游戏安装的 .NET；不需要提交或复制这些二进制文件。未设置变量时，脚本兼容原来的 Steam 目录布局。

产物为 `artifacts/ModernArsenal.tmod`。`ModernArsenal.csproj` 同样读取 `TML_PATH`，可用于编辑器和本地开发。

## 运行与测试

```powershell
.\tools\test.ps1             # 独立服务器回归，创建隔离测试世界
.\tools\test.ps1 -Reload     # 独立进程重载玩家和世界
.\tools\ciws-test.ps1        # 炮塔、平台、目标优先级、4500 RPM 和服务器校验
.\tools\ciws-test.ps1 -Reload
.\tools\play.ps1             # 缺少试玩存档时，初始化本地 Play/ 后启动
```

`Play/` 是各开发者自己的存档目录，`artifacts/` 放构建结果、隔离测试世界、报告和备份，均由 Git 忽略。源码仓库不带原开发者的角色与世界。测试不会覆盖默认 Terraria 存档。

`tools/play.ps1 -Ciws` 在试玩世界构建平台、交付遥控器并暂停；`-Flight` 把天界星盘和飞升之证装备到空闲饰品栏；`-Pause` 开无敌并暂停。它们是可选的本地试玩准备入口。

客户端回归还可运行 `tools/client-test.ps1`、`tools/presentation-test.ps1`。密集阵客户端测试 `tools/ciws-test.ps1 -Client` 需要先准备 `Play/Players`，并自行安装 `BossRushAPI`、`ExampleBossRush` 到 `Play/Mods`。这些第三方模组不会上传到本仓库。`tools/compat-test.ps1` 是原来的 ModernEngineering 联调入口，需要旁边另有该工程及其测试产物。

## 从哪里改

| 内容 | 入口 |
| --- | --- |
| 枪械、口径、弹药、进度、伤害 | `Data/arsenal.json`、`Catalog.cs` |
| 射击、装填、弹匣保存与同步 | `ArsenalGun.cs`、`ArsenalPlayer.cs`、`BallisticRound.cs` |
| 持枪、逐枪动作、枪灯和激光 | `GunVisuals.cs`、`GunAnimation.cs`、`Data/animations.json` |
| 补给箱、改枪界面 | `SupplyCrate.cs`、`ArmoryUI.cs`、`GunsmithUI.cs` |
| 密集阵射击、弹丸和遥控器 | `Phalanx.cs` |
| 平台布局、物品交付、世界保存 | `PhalanxArena.cs` |
| 四柱和各首领的目标优先级 | `PhalanxTargeting.cs` |
| 本地作弊与连战命令 | `ArsenalSandbox.cs` |
| 隔离验证、试玩准备 | `Verification/` |
| 原创像素素材生成 | `tools/generate_assets.py` |

改数值时同时考虑弹匣、换弹、散布、距离和护甲。大伤害在弹丸网络字段中使用受限载荷，通过 `ai[2]` 在护甲结算前还原；不要直接把超过原生字段范围的伤害塞进网络包。炮塔弹药类型保存在各发弹丸里，切弹不应改变已经射出的弹丸。

## 生成素材与图鉴

已生成的原创 `Assets/`、动作 GIF 和图鉴可直接使用。需要重新生成时：

```powershell
python -m pip install -r requirements-dev.txt
python tools/generate_assets.py
python tools/generate_docs.py
```

素材生成器使用 Windows 自带 Consolas 字体。图鉴可读取本地最新 `artifacts/balance-results.json`；首次克隆时回退到 `docs/verification/balance-1.2.0.json`。若改了枪械伤害，需先用 `-arsenalbalance <报告路径>` 重跑 `Verification/ArsenalBalanceAudit.cs`；生成器会拒绝沿用与当前伤害不匹配的旧结果。

参考照片可以自行重新检索到本地，但不影响构建，缺少照片也能生成图鉴。出处链接已保留；游戏反编译、下载的参考照片和第三方模组不应提交。

## 已知范围

现有快照覆盖服务器回归、角色和世界重载、持枪动画、改装、百万生命固定目标校准、密集阵射速及四柱原生消盾流程。真实多人联机和所有模组首领的完整通关尚未验收。地狱等特殊地形可能没有足够净空部署大阵地；可用备用炮塔在自建平台上布置，或使用已提供的飞行装备移动作战。
