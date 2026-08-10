using Xunit;

// ABP 集成测试每个测试类独立创建应用实例与 SQLite 内存库，
// 并行执行多个测试类会偶发死锁/挂起，统一串行执行保证确定性。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
