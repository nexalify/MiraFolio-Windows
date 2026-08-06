# MiraFolio for Windows 架构

## 项目结构

解决方案、源码目录、项目文件名和命名空间统一使用 `MiraFolio.*`：

```text
src/
├── MiraFolio.Core/   业务模型、图片选择、调度、显示器和壁纸服务
├── MiraFolio.App/    WPF 设置窗口、ViewModel、托盘和启动集成
└── MiraFolio.Tests/  调度、选图、设置和显示器过滤测试
```

应用对外名称、进程名、图标、窗口标题、托盘文案、发布产物和数据目录均为 `MiraFolio`。

## 启动流程

```text
App.OnStartup
├── 创建 MiraFolio 单实例互斥锁
├── 构建 Host 与依赖注入容器
├── 加载 MiraFolio 本地配置
├── 同步开机启动注册表项
├── RotationScheduler.Start
│   ├── 后台预热各显示器的图片文件夹
│   └── 为每台已配置显示器创建独立定时器
└── 初始化系统托盘；首次运行时打开设置窗口
```

## 核心数据流

### 设置

`SettingsViewModel` 读取活动显示器，将其实际坐标缩放为设置页中的拓扑预览。每台显示器对应一个 `WallpaperAssignment`。离线显示器已有配置会在保存时保留。

设置和全局图片排除清单写入 `%APPDATA%\MiraFolio\settings.json`，运行状态单独写入 `state.json`，避免每次换壁纸都触发设置变更和定时器重建。

### 定时轮替

```text
Timer / 立即轮替
└── RotationScheduler.RotateWallpaperAsync
    ├── 可选：检查该显示器上是否有全屏前台窗口
    ├── 按显示器串行化轮替，防止同屏并发设置
    ├── ImageSelector.SelectImage
    ├── WallpaperService.SetPosition + SetWallpaper
    ├── 更新当前壁纸与历史记录
    └── 通知设置页刷新缩略图和元数据
```

更换文件夹或重新启用显示器时会立即尝试轮替；如果后台扫描尚未完成，会在一个短窗口内重试选图。普通定时轮替从首个完整间隔后开始。

### 图片选择

`ImageSelector` 对每个壁纸根目录维护：

- 递归图片文件列表；
- 横图、竖图、方图方向桶；
- 不同方向和分辨率阈值的候选池；
- 正序与倒序列表；
- `FileSystemWatcher` 和 2 秒防抖重扫；
- 图片尺寸缓存和路径复用池。

扫描按 100 个文件分批处理，读取尺寸时最多使用 4 路并行。尺寸缓存保存在 `image_dim_cache.json`，下次启动只需为新增图片读取元数据。随机模式为每台显示器维护持久化洗牌队列，每轮候选图片只播放一次；新增文件随机插入当前轮剩余队列，删除文件从当前轮移除。文件夹、方向或分辨率过滤条件变化时才重建队列。顺序与倒序模式从当前壁纸的下一个位置继续。

用户从显示器预览移除图片时，只会在设置中增加一条 `RemovedImageRecord`，不会移动或删除源文件。调度器把全局排除路径传给图片选择器，随机、正序和倒序都在最终选择前过滤；随机队列会增量清掉本轮中的排除项，不需要重扫目录或重建尺寸缓存。回收站还原会删除排除记录，永久删除则在二次确认后删除源文件并清理记录。

### Windows 系统集成

- `MonitorService` 通过 `IDesktopWallpaper` 枚举显示器，并和 `Screen.AllScreens` 的活动边界交叉过滤幽灵/重复目标。单个 COM 目标失效时只跳过该目标，不会清空其余在线显示器；显示设置变化后会延迟刷新并在拓扑尚未稳定时自动重试。
- `DesktopWallpaperHost` 统一管理专用 STA 线程与 `IDesktopWallpaper` COM 实例，`MonitorService` 和 `WallpaperService` 共享该宿主，避免重复初始化和固定延时等待。
- `TrayIconSetup` 提供设置、立即轮替全部壁纸和退出入口。
- `StartupManager` 管理当前用户的 `Run` 注册表项。

## 持久化

| 文件 | 作用 |
|---|---|
| `settings.json` | 全局设置、逐显示器分配与已移除图片清单 |
| `state.json` | 当前壁纸、上次轮替时间、近期历史、随机播放轮次队列 |
| `image_dim_cache.json` | 图片尺寸与根目录/相对路径映射 |
| `mirafolio.log` | 最大 5 MB 的运行日志，轮转保留一个 `.bak` |

文件默认位于 `%APPDATA%\MiraFolio`。

## 已知边界

- 仅支持 Windows 10 / 11，运行和 UI 验证应在 Windows 上完成。
- `MonitorService` 订阅 `SystemEvents.DisplaySettingsChanged`，在显示器增减、分辨率或排列变化后重新读取拓扑。已保存配置的显示器重新接入后，调度器会立即预热图片目录并尝试应用一次壁纸；全新显示器需先在设置页选择图片目录。
- Windows 版尚未接入 iOS 版的私密相册、AI 标签、Wi-Fi 传图和商业化能力。
