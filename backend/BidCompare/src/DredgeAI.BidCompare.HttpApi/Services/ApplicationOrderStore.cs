using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DredgeAI.BidCompare.Applications;

/// <summary>
/// 应用展示顺序存储（JSON 文件持久化，后端重启不丢）：
/// - admin 默认顺序：主应用 id 列表 + 每个主应用下的子应用 id 列表（子项只能在母项组内上移/下移）；
/// - 用户个性化顺序：按用户 id 保存的 route 顺序列表，只有用户显式调整过才存在；
/// 数据落盘到 Host 的 App_Data/app-order.json；文件损坏时自动回退为空，不阻断启动。
/// </summary>
public class ApplicationOrderStore
{
    private readonly object _lock = new();
    private readonly string _filePath;
    private readonly ConcurrentDictionary<Guid, string[]> _userOrders = new();
    private readonly Dictionary<string, string[]> _subOrders = new();
    private string[] _adminOrder = Array.Empty<string>();
    private bool _loaded;

    public ApplicationOrderStore(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>返回默认顺序快照：主应用顺序 + 各母项的子应用顺序。</summary>
    public (string[] AppIds, Dictionary<string, string[]> SubOrders) GetDefaultOrder()
    {
        lock (_lock)
        {
            EnsureLoaded();
            return (_adminOrder.ToArray(), _subOrders.ToDictionary(x => x.Key, x => x.Value.ToArray()));
        }
    }

    /// <summary>合并默认顺序：保留已有位置，仅追加新出现的 id（幂等，供前端每次加载调用）。</summary>
    public void MergeAdminOrder(string[] appIds, Dictionary<string, string[]> subOrders)
    {
        lock (_lock)
        {
            EnsureLoaded();
            var known = new HashSet<string>(_adminOrder);
            var merged = _adminOrder.ToList();
            foreach (var id in appIds)
            {
                if (string.IsNullOrWhiteSpace(id) || !known.Add(id))
                {
                    continue;
                }

                merged.Add(id);
            }

            _adminOrder = merged.ToArray();

            foreach (var (parentId, subs) in subOrders)
            {
                if (string.IsNullOrWhiteSpace(parentId) || subs == null)
                {
                    continue;
                }

                _subOrders.TryGetValue(parentId, out var existing);
                var subKnown = new HashSet<string>(existing ?? Array.Empty<string>());
                var mergedSubs = (existing ?? Array.Empty<string>()).ToList();
                foreach (var sub in subs)
                {
                    if (string.IsNullOrWhiteSpace(sub) || !subKnown.Add(sub))
                    {
                        continue;
                    }

                    mergedSubs.Add(sub);
                }

                _subOrders[parentId] = mergedSubs.ToArray();
            }

            Save();
        }
    }

    /// <summary>上移/下移一位：子应用在母项组内移动，主应用在全局顺序中移动，返回重排后的默认顺序。</summary>
    public (string[] AppIds, Dictionary<string, string[]> SubOrders) Move(string id, bool up)
    {
        lock (_lock)
        {
            EnsureLoaded();
            foreach (var (parentId, subs) in _subOrders)
            {
                var list = subs.ToList();
                var index = list.FindIndex(x => x == id);
                if (index < 0)
                {
                    continue;
                }

                var target = up ? index - 1 : index + 1;
                if (target >= 0 && target < list.Count)
                {
                    (list[index], list[target]) = (list[target], list[index]);
                    _subOrders[parentId] = list.ToArray();
                }

                Save();
                return Snapshot();
            }

            var mainList = _adminOrder.ToList();
            var mainIndex = mainList.FindIndex(x => x == id);
            if (mainIndex < 0)
            {
                mainList.Add(id);
                mainIndex = mainList.Count - 1;
            }

            var mainTarget = up ? mainIndex - 1 : mainIndex + 1;
            if (mainTarget >= 0 && mainTarget < mainList.Count)
            {
                (mainList[mainIndex], mainList[mainTarget]) = (mainList[mainTarget], mainList[mainIndex]);
            }

            _adminOrder = mainList.ToArray();
            Save();
            return Snapshot();
        }
    }

    public string[]? GetUserOrder(Guid userId)
    {
        lock (_lock)
        {
            EnsureLoaded();
            return _userOrders.TryGetValue(userId, out var order) ? order.ToArray() : null;
        }
    }

    public void SetUserOrder(Guid userId, string[] routeIds)
    {
        lock (_lock)
        {
            EnsureLoaded();
            _userOrders[userId] = routeIds.ToArray();
            Save();
        }
    }

    public int ResetUserOrders()
    {
        lock (_lock)
        {
            EnsureLoaded();
            var count = _userOrders.Count;
            _userOrders.Clear();
            Save();
            return count;
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        try
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<PersistedData>(json);
            if (data == null)
            {
                return;
            }

            _adminOrder = data.AdminOrder ?? Array.Empty<string>();
            _subOrders.Clear();
            foreach (var (key, value) in data.SubOrders ?? new Dictionary<string, string[]>())
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    _subOrders[key] = value ?? Array.Empty<string>();
                }
            }

            _userOrders.Clear();
            foreach (var (key, value) in data.UserOrders ?? new Dictionary<Guid, string[]>())
            {
                _userOrders[key] = value ?? Array.Empty<string>();
            }
        }
        catch
        {
            // 文件缺失/损坏时回退为空，不阻断启动
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var data = new PersistedData
            {
                AdminOrder = _adminOrder,
                SubOrders = _subOrders.ToDictionary(x => x.Key, x => x.Value.ToArray()),
                UserOrders = _userOrders.ToDictionary(x => x.Key, x => x.Value.ToArray()),
            };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // 持久化失败不影响运行
        }
    }

    private (string[] AppIds, Dictionary<string, string[]> SubOrders) Snapshot()
        => (_adminOrder.ToArray(), _subOrders.ToDictionary(x => x.Key, x => x.Value.ToArray()));

    private sealed class PersistedData
    {
        public string[]? AdminOrder { get; set; }

        public Dictionary<string, string[]>? SubOrders { get; set; }

        public Dictionary<Guid, string[]>? UserOrders { get; set; }
    }
}
