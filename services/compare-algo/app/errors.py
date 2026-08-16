"""服务端内部异常类型：区别于调用方数据错误（422），统一映射 500。"""


class EvidenceBuildError(RuntimeError):
    """服务端组装 Evidence 失败（内部不变量破坏，属服务端 bug）。

    典型场景：分析管线产出的 locations/docIds 为空列表，触发 Evidence 模型
    min_length=1 校验。与产物校验失败的 pydantic.ValidationError（→422）区分，
    main.py 为其注册 500 处理器，不让调用方为服务端 bug 背锅。
    """
