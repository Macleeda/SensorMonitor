---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 215d5fbe0e1875395a2f554637463daf_c945dd0a7b6511f18401525400bff409
    ReservedCode1: RTZ6zCCyRYdbeZKUmQafsDoUE6fep3nC8WmSpCriW81LRfjQNnPtQT8ZCRPstWGwiFZPKaDedIPfmaH/z3ipNp5bIwM8w7Kq8FJSARBQot0cOPqwJnJEbBNSKAbi8aqrsKItxEHDzZHJ10nYzsMfB41h8WS2+JvI5JrjhzWuzria8xOyBYcywZCbOOI=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 215d5fbe0e1875395a2f554637463daf_c945dd0a7b6511f18401525400bff409
    ReservedCode2: RTZ6zCCyRYdbeZKUmQafsDoUE6fep3nC8WmSpCriW81LRfjQNnPtQT8ZCRPstWGwiFZPKaDedIPfmaH/z3ipNp5bIwM8w7Kq8FJSARBQot0cOPqwJnJEbBNSKAbi8aqrsKItxEHDzZHJ10nYzsMfB41h8WS2+JvI5JrjhzWuzria8xOyBYcywZCbOOI=
---



# 多通道温湿度数据采集与实时监控上位机

## 技术栈
C# (.NET Framework 4.8) + WinForms + SQLite + TCP Socket + Chart + GDI+

## 项目概述
本项目模拟工业上位机场景，实现4通道传感器数据的实时采集、波形显示、数据库存储、阈值报警与TCP远程监控。
无需真实硬件，内置虚拟数据模拟器即可完整运行演示。

## 亮点

| 序号 | 亮点 | 涉及技能 |
|------|------|----------|
| 1 | 生产者-消费者模式 — 数据采集线程与UI线程完全解耦 | 多线程、并发编程 |
| 2 | 自定义GDI+圆环仪表盘控件 — 手绘弧形进度+动态指针 | 面向对象、GDI+绘图 |
| 3 | Chart实时波形显示 — 4通道Spline曲线，自动滚动 | WinForms Chart控件 |
| 4 | SQLite数据持久化 — 批量写入优化，索引优化的查询 | SQL/SQLite |
| 5 | TCP Socket远程监控 — 异步Accept多客户端连接 | TCP/IP、Socket编程 |
| 6 | 阈值报警机制 — 超限变色闪烁+日志记录 | 事件驱动、状态机 |
| 7 | 历史数据查询 + CSV导出 | 数据库查询、文件IO |

## 运行方式

### 前置条件
- Visual Studio 2019/2022（任意版本）
- .NET Framework 4.8 SDK（Windows 10自带）

### 步骤
1. 双击 `SensorMonitor.csproj` 用 Visual Studio 打开
2. NuGet 包会自动还原（SQLite + Chart）
3. 按 `F5` 运行
4. 点击「开始采集」即可看到4通道数据实时滚动

### 功能按钮
- **开始采集** — 启动虚拟数据源，仪表盘和Chart开始实时刷新
- **停止采集** — 暂停数据源
- **历史查询** — 弹出近1小时的数据库记录
- **导出CSV** — 将近2小时数据导出为CSV文件
- **启动TCP** — 在8899端口开启TCP监听，可用telnet测试连接

## 项目结构

```
SensorMonitor/
├── SensorMonitor.csproj    # 项目文件
├── Program.cs              # 入口
├── FormMain.cs             # 主界面（UI+业务逻辑 ~450行）
├── DbManager.cs            # SQLite 数据库管理
├── SerialSimulator.cs      # 虚拟串口数据模拟器
├── CircularGauge.cs        # 自定义圆环仪表盘控件
└── TcpServerHelper.cs      # TCP远程监控服务
```

## 描述

**多通道温湿度数据采集与实时监控上位机** | C# WinForms | 独立开发
- 基于C# WinForms开发，实现4通道传感器数据的实时采集、波形显示与SQLite持久化存储
- 采用生产者-消费者设计模式，将数据采集线程与UI渲染线程解耦，通过Queue缓冲区实现线程安全的数据传递
- 使用GDI+自绘圆环仪表盘控件，支持弧形进度绘制、动态指针与报警闪烁效果
- 集成Chart控件实现多通道Spline曲线实时渲染，支持200点滑动窗口
- 基于TCP Socket实现局域网远程监控服务，支持异步多客户端连接
- 实现阈值报警机制：温度超35℃或湿度超85%时界面闪烁告警并记录日志
- 支持历史数据按时间范围查询与CSV批量导出
