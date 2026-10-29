# XFMCL Open（XFMCL 开源版）

跨平台 Minecraft Java 版启动器。桌面端只做两件事：**下载游戏文件**、**离线启动游戏**；Android 端提供**版本下载与文件管理**。

- 开源协议：MIT
- 版权归属：玹砺科技 XUANFORGE
- 本项目与 Mojang Studios / Microsoft 无任何关联

## 功能

### 桌面端（Windows / Linux / macOS）

- **离线登录**：玩家名在本地按 `OfflinePlayer:<name>` 规则生成 v3 UUID，不请求任何账号服务器。
- **版本列表**：读取 Mojang 版本清单，支持正式版 / 快照，支持关键字搜索。
- **下载 / 补全**：client.jar、libraries、natives、assets、日志配置，SHA1 校验，已完成的文件自动跳过。
- **双下载源**：官方源与镜像源（地址全部从配置读取），单个文件失败时自动切换备用地址。
- **自动探测 Java**：覆盖 Windows / Linux / macOS 常见安装位置，按所选版本要求挑选运行时。
- **启动与日志**：启动游戏进程并把标准输出实时显示在界面内。

### Android 端

- 版本清单浏览、搜索与离线玩家名（本地 UUID）。
- 下载 client.jar、非 natives 游戏库、资源索引与资源文件，保存在应用专属目录。
- **不提供启动功能**：在 Android 上运行 Minecraft Java 版需要移植版 JVM，不在本项目范围内；natives 属于桌面系统二进制，也由桌面端补齐。

## 明确不支持

- 微软正版账号登录、皮肤、Realms
- Forge / Fabric / Quilt 等加载器安装
- 1.7 之前的旧版资源结构（`assets` 为 `legacy` 的版本）
- 模组管理、整合包导入、启动器自动更新
- Android 端启动游戏

## 技术栈与依赖

### 桌面端

| 组件 | 版本 | 说明 |
| --- | --- | --- |
| .NET | 10.0 | 目标框架 `net10.0` |
| Avalonia | 12.1.4 | 跨平台 UI |
| Avalonia.Desktop | 12.1.4 | 桌面窗口后端 |
| Avalonia.Themes.Fluent | 12.1.4 | 主题 |
| Avalonia.Fonts.Inter | 12.1.4 | 内置字体 |

JSON、下载、哈希校验、进程管理全部使用 .NET 内置 API，没有引入其它第三方库；所有 NuGet 版本均写死，不使用浮动版本。

### Android 端

| 组件 | 版本 | 说明 |
| --- | --- | --- |
| AGP | 8.5.0 | 与 XFMCL Android 保持一致 |
| Kotlin | 2.0.21 | 语言与 Compose 编译器插件 |
| Compose BOM | 2024.10.00 | material3 / foundation / ui |
| activity-compose | 1.9.3 | Compose Activity 入口 |
| kotlinx-coroutines-android | 1.9.0 | 并发下载与进度 |
| compileSdk / minSdk / targetSdk | 36 / 26 / 34 | 与 XFMCL Android 一致 |

网络使用 `java.net.HttpURLConnection`，JSON 使用 Android 内置 `org.json`，没有引入 OkHttp、Gson 等额外依赖。

## 项目结构

```
XFMCL 开源版/
├─ Xfmcl.Open.slnx            桌面解决方案
├─ config/
│  └─ endpoints.example.json   下载端点模板（复制后填写）
├─ LICENSE
├─ README.md
├─ 修改记录.md
├─ src/
│  ├─ Xfmcl.Open.Core/         版本清单、下载、文件补全、Java 探测、离线身份、启动编排
│  ├─ Xfmcl.Open.UI/           跨平台界面（MainView）、设置、版本行模型
│  └─ Xfmcl.Open.Desktop/      Windows / Linux / macOS 桌面入口
└─ android/                    Android 原生工程（Kotlin + Jetpack Compose）
   ├─ settings.gradle.kts
   ├─ build.gradle.kts
   ├─ signing.properties.example  签名配置模板（实际配置不入库）
   └─ app/
      └─ src/main/java/cn/xuanforge/xfmclopen/
         ├─ data/              清单、下载、补全、离线身份
         └─ ui/                Compose 界面
```

## 构建与运行

### 桌面端

需要 .NET 10 SDK。

```powershell
dotnet build Xfmcl.Open.slnx -c Release
dotnet run --project src/Xfmcl.Open.Desktop
```

发布单文件（自包含运行时，目标机器无需安装 .NET）：

```powershell
dotnet publish src/Xfmcl.Open.Desktop -c Release -r win-x64
dotnet publish src/Xfmcl.Open.Desktop -c Release -r linux-x64
dotnet publish src/Xfmcl.Open.Desktop -c Release -r osx-x64
dotnet publish src/Xfmcl.Open.Desktop -c Release -r osx-arm64
```

产物位于 `src/Xfmcl.Open.Desktop/bin/Release/<目标框架>/<RID>/publish/`，Windows 裁剪后单文件约 23 MB。

### Android

需要 JDK 17 或 21 与 Android SDK。

```powershell
# 1. 指向本机 SDK（该文件不入库）
#    android/local.properties: sdk.dir=/path/to/Android/Sdk

# 2. 准备签名配置（可选，缺失时会自动跳过签名）
#    copy android/signing.properties.example android/signing.properties
#    并把 credentialsFile 指向本机的 keystore.properties

cd android
.\gradlew.bat :app:assembleDebug      # 调试包
.\gradlew.bat :app:assembleRelease    # 发布包（R8 + 正式签名）
```

- `signing.properties`、`local.properties` 均被 `.gitignore` 忽略，仓库里只保留 `signing.properties.example` 模板。
- `signing.properties` 只保存凭据文件路径，密码从外部 `keystore.properties` 读取，不进入仓库；凭据缺失时自动跳过签名，构建不会失败。
- 发布包使用 `CN=XuanForge` 证书，出包后可用 `apksigner verify --print-certs` 校验。
- 当前 release APK 约 1.1 MB，debug APK 约 8.4 MB。

## 下载端点配置（必须）

仓库内**不包含任何真实下载地址**，所有端点默认是 `example.com` 占位符。首次使用前必须配置，否则启动器只显示界面，不会发起下载请求。

字段说明：

| 字段 | 用途 |
| --- | --- |
| `manifestUrl` | 版本清单 JSON 地址 |
| `metaRoot` | 版本元数据根地址 |
| `dataRoot` | 版本数据根地址（client.jar 等） |
| `librariesRoot` | 依赖库根地址 |
| `resourcesRoot` | 资源文件根地址 |
| `mirrorRoot` | 镜像源根地址 |

桌面端：把 `config/endpoints.example.json` 复制到配置目录并改名为 `endpoints.json`：

- Windows：`%APPDATA%\XFMCL-Open\endpoints.json`
- Linux：`~/.config/XFMCL-Open/endpoints.json`
- macOS：`~/Library/Application Support/XFMCL-Open/endpoints.json`

Android 端：修改 `android/app/src/main/assets/endpoints.json` 后重新构建 APK。

## 配置与数据

- 桌面配置：`%APPDATA%\XFMCL-Open\settings.ini`（Linux 为 `~/.config/XFMCL-Open/`，macOS 为 `~/Library/Application Support/XFMCL-Open/`）
- 桌面默认游戏目录：Windows `%APPDATA%\.minecraft`，macOS `~/Library/Application Support/minecraft`，Linux `~/.minecraft`
- Android 数据目录：`Android/data/cn.xuanforge.xfmclopen/files/minecraft`

## 参与开源

- 提交前请确认 `dotnet build Xfmcl.Open.slnx -c Release` 与 `gradlew :app:assembleDebug` 均能通过。
- 仓库不包含任何密钥、证书或账号凭据；请不要在 Issue / PR 中粘贴 `keystore.properties`、密码或令牌。
- 本项目只实现离线身份，不接受“正版登录”“绕过鉴权”类改动。
- 代码风格：C# 使用可空引用类型与源生成 JSON；Kotlin 使用官方风格并保持依赖版本锁定。

## 第三方许可

| 依赖 | 许可 |
| --- | --- |
| Avalonia / Avalonia.Desktop / Avalonia.Themes.Fluent / Avalonia.Fonts.Inter | MIT |
| .NET 运行时 | MIT |
| AndroidX / Jetpack Compose / kotlinx.coroutines | Apache-2.0 |
| Kotlin / Android Gradle Plugin | Apache-2.0 |
| Inter 字体 | SIL Open Font License 1.1 |

本项目自身以 MIT 发布，上述依赖与字体保留各自许可。再分发（包括闭源分发）时必须保留本项目的版权声明与 LICENSE 文本；游戏资源不包含在本仓库。

## 版权

Copyright (C) 2026 玹砺科技 XUANFORGE

本项目以 MIT 协议开源，详见 [LICENSE](LICENSE)。再分发（包括闭源分发）时必须保留上述版权声明与 LICENSE 文本。

本项目仅用于技术学习与个人使用，不提供任何游戏本体或账号。下载的游戏文件版权归 Mojang Studios / Microsoft 所有，使用前请确保已通过正规渠道购买 Minecraft。
