# Core 接口

## 职责

Core 提供纯 C# 基础工具。`MxFramework.Core` 不依赖 UnityEngine；Unity 类型相关工具放在 `MxFramework.Core.Unity`。

## 公开类型

| 类型 | 位置 | 用途 |
|------|------|------|
| `IHeapItem<T>` / `Heap<T>` | `Core/Collections/Heap.cs` | 固定容量堆 |
| `IUnsortListItem` / `UnsortList<T>` | `Core/Collections/UnsortList.cs` | 支持延迟删除的无序列表 |
| `RingBuffer<T>` | `Core/Collections/RingBuffer.cs` | 固定容量最近 N 条缓冲，按旧到新复制 |
| `StableHandle` | `Core/Handles/StableHandle.cs` | generation handle，防止 stale handle 命中新对象 |
| `StableHandleTable<T>` / `StableHandleTableSnapshot` | `Core/Handles/StableHandleTable.cs` | 通用 stable handle 表，支持 add / try get / remove / clear |
| `ObjectPool<T>` | `Core/Pooling/ObjectPool.cs` | 实例对象池，支持回调、预热、max inactive 和重复释放检测 |
| `IReference` / `ReferencePool<T>` | `Core/Pooling/ReferencePool.cs` | 可清理引用对象池，`Release` 时调用 `Clear()` |
| `ListPool<T>` / `PooledList<T>` | `Core/Pooling/ListPool.cs` | 临时 `List<T>` 池，支持 `using` 自动归还 |
| `BitUtils` | `Core/Math/BitUtils.cs` | int 打包、拆包、位运算辅助 |
| `zstring` | `Core/Extensions/ZString.cs` | 低分配字符串拼接 |
| `RandomTable` | `Core.Unity/RandomTable.cs` | Unity 随机查表 |
| `VectorExtensions` | `Core.Unity/VectorExtensions.cs` | Vector 角度、方向、长度扩展 |

## 使用约定

- `Heap<T>` 的元素必须维护 `HeapIndex`。
- `UnsortList<T>.RemoveDelayed` 后需要调用 `Optimize` 才真正压缩列表。
- `StableHandleTable<T>` 的旧 handle 在 remove / clear / slot 复用后必须失效。
- `ObjectPool<T>` / `ReferencePool<T>` 不提供隐式全局池；由组合根或模块显式持有。
- `ListPool<T>` 只用于临时集合；`PooledList<T>.Dispose()` 后不能继续访问 `List`。
- `RingBuffer<T>.CopyTo()` 会追加到输出列表，输出顺序为旧到新。
- Unity 类型只能放在 `Core.Unity`。
- Core 不承载游戏 ID、属性含义或业务规则。

## 测试入口

`Assets/Scripts/MxFramework/Tests/Core/`
