"""Send WeCom webhook notification for DredgeAI release.

触发方式：推 master（`pnpm release` 会同时推 master 与 v* tag，只由 master 那条通知，
见 .github/workflows/release-notify.yml），一次推送一条消息。

消息内容：版本号 + 版本区间 + CHANGELOG 该版本段（无该段则按类型聚合提交）。
本地预览：DRY_RUN=1 python .github/scripts/webhook_notify.py
"""
import json
import os
import re
import subprocess
import sys
import urllib.request
from datetime import datetime

# repo root is parent of .github/scripts/ directory
repo = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# 按 conventional-commit 类型分组，顺序即展示顺序
GROUPS = [
    ("feat", "✨ 新功能"),
    ("fix", "🐛 修复"),
    ("perf", "⚡ 性能"),
    ("refactor", "♻️ 重构"),
    ("docs", "📝 文档"),
    ("test", "🧪 测试"),
    ("ci", "🔧 构建/CI"),
    ("build", "🔧 构建/CI"),
    ("chore", "🧰 杂项"),
]
GROUP_TITLES = dict(GROUPS)
# 多个 commit 类型可能归并到同一标题（如 ci/build），展示顺序按标题去重
GROUP_ORDER = list(dict.fromkeys(title for _, title in GROUPS))
MAX_PER_GROUP = 3
# 企业微信 markdown 上限 4096 字节，留出余量
MAX_BYTES = 3500
COMMIT_RE = re.compile(r"^(?P<type>[a-zA-Z]+)(?:\((?P<scope>[^)]*)\))?!?:\s*(?P<desc>.+)$")


def git(*args: str, allow_fail: bool = False) -> str:
    """执行 git 并返回 stdout；失败返回空串（allow_fail 标记无 tag 仓库等预期内探测）。"""
    proc = subprocess.run(
        ["git", *args],
        cwd=repo,
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
    )
    if proc.returncode != 0:
        if not allow_fail:
            print(f"git {' '.join(args)} 退出码 {proc.returncode}，按空值处理", file=sys.stderr)
        return ""
    return proc.stdout.decode(errors="replace").strip()


def git_ok(*args: str) -> bool:
    """执行 git 并返回是否成功（cat-file -e 之类无输出的探测用）。"""
    return (
        subprocess.run(
            ["git", *args],
            cwd=repo,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        ).returncode
        == 0
    )


def read_version() -> str:
    """工作区版本号：根 package.json 的 version。"""
    try:
        with open(os.path.join(repo, "package.json"), encoding="utf-8-sig") as f:
            return str(json.load(f).get("version", "")).strip()
    except (OSError, ValueError):
        return ""


def resolve_refs() -> tuple[str, str, str]:
    """返回 (版本号, 版本区间起点描述, 提交范围起点)。

    版本号：HEAD 恰好带 tag 时取 tag（发版提交本来就带 tag），否则取根 package.json
    ——两者口径一致，CI 也是从 package.json 读版本拼镜像号。
    提交范围：优先用 GITHUB 给的 PREV_SHA（普通 master 推送即本次推送的提交）；
    手动 dispatch 没有 PREV_SHA 时退化为「上一个 tag → HEAD」。
    """
    exact_tag = git("describe", "--tags", "--exact-match", "HEAD", allow_fail=True)
    version = exact_tag.lstrip("vV") if exact_tag else read_version()

    prev_sha = os.environ.get("PREV_SHA", "").strip()
    base = ""
    base_from_prev_sha = False
    if prev_sha and git_ok("cat-file", "-e", f"{prev_sha}^{{commit}}"):
        base = prev_sha
        base_from_prev_sha = True
    elif exact_tag:
        base = git("describe", "--tags", "--abbrev=0", f"{exact_tag}^", allow_fail=True)
    else:
        base = git("describe", "--tags", "--abbrev=0", "HEAD", allow_fail=True)

    if base and base_from_prev_sha:
        base_desc = base[:7]
    elif base:
        base_desc = git("describe", "--tags", "--abbrev=0", base, allow_fail=True) or base[:7]
    else:
        base_desc = git("rev-list", "--max-parents=0", "HEAD")[:7]
    return version, base_desc, base


def changelog_bullets(version: str, max_items: int = 8) -> list[str]:
    """取 CHANGELOG.md 中该版本的条目；作者精修过就用它，否则回退到自动聚合。"""
    if not version:
        return []
    path = os.path.join(repo, "CHANGELOG.md")
    if not os.path.isfile(path):
        return []
    try:
        with open(path, encoding="utf-8-sig") as f:
            text = f.read()
    except OSError:
        return []
    heading = re.compile(rf"^##\s+v?{re.escape(version)}\s*$")
    items: list[str] = []
    inside = False
    for raw in text.splitlines():
        line = raw.strip()
        if line.startswith("## "):
            inside = bool(heading.match(line))
            continue
        if inside and line.startswith("- "):
            item = line[2:].strip()
            if item and not item.startswith("…"):
                items.append(item)
            if len(items) >= max_items:
                break
    return items


def collect_changelog(base: str) -> tuple[list[str], int]:
    """聚合 base..HEAD 的提交为分类 CHANGELOG 行，返回 (行列表, 提交总数)。"""
    rng = f"{base}..HEAD" if base else "HEAD"
    subjects = [s for s in git("log", "--no-merges", "--format=%s", rng).splitlines() if s.strip()]
    grouped: dict[str, list[str]] = {}
    other: list[str] = []
    for subject in subjects:
        match = COMMIT_RE.match(subject)
        if not match:
            other.append(subject)
            continue
        title = GROUP_TITLES.get(match.group("type").lower())
        if not title:
            other.append(subject)
            continue
        scope = match.group("scope")
        desc = match.group("desc").strip()
        grouped.setdefault(title, []).append(f"[{scope}] {desc}" if scope else desc)

    lines: list[str] = []
    for title in GROUP_ORDER:
        items = grouped.get(title)
        if not items:
            continue
        # 标题带总数：即使只展示前几条，也不隐藏"还有多少"
        lines.append(f"**{title}（{len(items)}）**")
        lines.extend(f"> - {item}" for item in items[:MAX_PER_GROUP])
    if other:
        lines.append(f"**🧾 其他（{len(other)}）**")
        lines.extend(f"> - {item}" for item in other[:MAX_PER_GROUP])
    return trim_to_bytes(lines), len(subjects)


def trim_to_bytes(lines: list[str], tail: str = "") -> list[str]:
    """按 UTF-8 字节上限裁剪明细行（保留每个分类标题）。"""
    kept = list(lines)
    while kept and len("\n".join(kept + [tail]).encode("utf-8")) > MAX_BYTES:
        # 从末尾开始丢明细行（`> - ` 开头），分类标题尽量保留
        index = next((i for i in range(len(kept) - 1, -1, -1) if kept[i].startswith("> - ")), None)
        if index is None:
            kept.pop()
        else:
            kept.pop(index)
    return kept


webhook = os.environ.get("WEBHOOK", "")
dry_run = os.environ.get("DRY_RUN", "").strip() not in ("", "0", "false")
if not webhook and not dry_run:
    print("WEBHOOK not set, skipping")
    sys.exit(0)

run_url = os.environ.get("RUN_URL", "")
version, base_desc, base = resolve_refs()
auto_lines, total = collect_changelog(base)
curated = changelog_bullets(version)
if curated:
    changelog_lines = [f"**📋 CHANGELOG（v{version}）**"]
    changelog_lines += [f"> - {item}" for item in curated]
    changelog_lines = trim_to_bytes(changelog_lines)
else:
    changelog_lines = auto_lines
sha = git("log", "-1", "--format=%h")
ref = os.environ.get("GITHUB_REF_NAME") or git("rev-parse", "--abbrev-ref", "HEAD")

lines = ["## ✅ DredgeAI 发版完成"]
if version:
    lines.append(f"> **版本:** `v{version}`")
if base_desc:
    lines.append(f"> **区间:** `{base_desc}` → `{sha}`（{total} 个提交）")
lines.append("")
lines.extend(changelog_lines)
lines += [
    "",
    f"> **分支:** `{ref}`",
    f"> **时间:** `{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}`",
]
if run_url:
    lines.append(f"\n[查看 Actions]({run_url})")
content = "\n".join(lines)

if dry_run:
    print(content)
    sys.exit(0)

payload = json.dumps(
    {"msgtype": "markdown", "markdown": {"content": content}},
    ensure_ascii=False,
).encode("utf-8")
req = urllib.request.Request(
    webhook,
    data=payload,
    headers={"Content-Type": "application/json; charset=utf-8"},
)
resp = urllib.request.urlopen(req)
print("WeCom notify status:", resp.status)
