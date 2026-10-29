/* XFMCL Open - HTML 模板逻辑
 * 玹砺科技 XUANFORGE
 *
 * 仅做界面；下载、校验、进度均通过 JS Bridge 调用 Android 宿主。
 * 本项目以 MIT 协议开源。
 */

(function () {
  "use strict";

  var bridge = window.XfmclAndroid || null;

  var state = {
    versions: [],
    selected: null,
    filter: "",
    busy: false,
    configured: false,
    source: "OFFICIAL",
    player: "Player"
  };

  function $(id) {
    return document.getElementById(id);
  }

  function parse(json) {
    try {
      return JSON.parse(json);
    } catch (error) {
      return null;
    }
  }

  function setStatus(text) {
    $("status").textContent = text;
  }

  function setBusy(busy) {
    state.busy = busy;
    $("downloadButton").disabled = busy || !state.selected || !state.configured;
    $("refreshButton").disabled = busy;
    $("cancelButton").classList.toggle("hidden", !busy);
    $("downloadButton").textContent = busy ? "正在处理…" : "下载 / 补全文件";
  }

  function formatBytes(bytes) {
    if (!bytes || bytes < 0) {
      return "0 B";
    }

    if (bytes < 1024) {
      return bytes + " B";
    }

    if (bytes < 1024 * 1024) {
      return (bytes / 1024).toFixed(1) + " KB";
    }

    return (bytes / (1024 * 1024)).toFixed(1) + " MB";
  }

  function setProgress(completed, total, bytes) {
    var ratio = total > 0 ? Math.min(1, completed / total) : 0;
    $("progressBar").style.width = (ratio * 100).toFixed(1) + "%";
    $("progressText").textContent = completed + "/" + total + " · " + formatBytes(bytes);
  }

  function log(line) {
    var text = String(line || "");
    if (!text) {
      return;
    }

    var time = new Date().toLocaleTimeString("zh-CN", { hour12: false });
    var logEl = $("log");
    var next = logEl.textContent + time + " " + text + "\n";
    var lines = next.split("\n");
    if (lines.length > 160) {
      lines = lines.slice(lines.length - 160);
    }

    logEl.textContent = lines.join("\n");
    logEl.scrollTop = logEl.scrollHeight;
  }

  function toast(message) {
    var el = $("toast");
    el.textContent = message;
    el.classList.remove("hidden");
    window.clearTimeout(toast.timer);
    toast.timer = window.setTimeout(function () {
      el.classList.add("hidden");
    }, 2200);
  }

  function typeLabel(type) {
    switch (type) {
      case "release":
        return "正式版";
      case "snapshot":
        return "快照";
      case "old_beta":
        return "旧测试版";
      case "old_alpha":
        return "远古版";
      default:
        return type || "版本";
    }
  }

  function visibleVersions() {
    if (!state.filter) {
      return state.versions;
    }

    var keyword = state.filter.toLowerCase();
    return state.versions.filter(function (item) {
      return item.id.toLowerCase().indexOf(keyword) >= 0;
    });
  }

  function renderVersions() {
    var list = $("versionList");
    var rows = visibleVersions();
    list.textContent = "";

    if (rows.length === 0) {
      var empty = document.createElement("div");
      empty.className = "empty";
      empty.textContent = state.versions.length === 0 ? "暂无版本，请先刷新" : "没有匹配的版本";
      list.appendChild(empty);
      return;
    }

    rows.forEach(function (item) {
      var row = document.createElement("div");
      row.className = "version-item" + (item.id === state.selected ? " selected" : "");
      row.setAttribute("role", "option");
      row.setAttribute("aria-selected", item.id === state.selected ? "true" : "false");

      var accent = document.createElement("span");
      accent.className = "accent";

      var meta = document.createElement("span");
      meta.className = "meta";

      var id = document.createElement("div");
      id.className = "id";
      id.textContent = item.id;

      var sub = document.createElement("div");
      sub.className = "sub";
      sub.textContent = typeLabel(item.type) + " · " + (item.date || "—");

      meta.appendChild(id);
      meta.appendChild(sub);
      row.appendChild(accent);
      row.appendChild(meta);

      if (item.installed) {
        var badge = document.createElement("span");
        badge.className = "badge";
        badge.textContent = "已安装";
        row.appendChild(badge);
      }

      row.addEventListener("click", function () {
        if (state.busy) {
          return;
        }

        state.selected = item.id;
        renderVersions();
        renderSelection();
      });

      list.appendChild(row);
    });
  }

  function renderSelection() {
    var selected = null;
    for (var i = 0; i < state.versions.length; i += 1) {
      if (state.versions[i].id === state.selected) {
        selected = state.versions[i];
        break;
      }
    }

    $("selectedVersion").textContent = selected ? selected.id : "—";
    $("selectedState").textContent = selected ? (selected.installed ? "已安装" : "未安装") : "未选择";
    $("selectedState").style.color = selected && selected.installed ? "var(--ok)" : "";
    $("downloadButton").disabled = state.busy || !selected || !state.configured;
  }

  function selectDefault() {
    if (state.selected) {
      return;
    }

    var latest = null;
    for (var i = 0; i < state.versions.length; i += 1) {
      if (state.versions[i].type === "release") {
        latest = state.versions[i];
        break;
      }
    }

    if (!latest && state.versions.length > 0) {
      latest = state.versions[0];
    }

    if (latest) {
      state.selected = latest.id;
    }
  }

  function updateSourceButtons() {
    var buttons = document.querySelectorAll("#sourceSwitch button");
    Array.prototype.forEach.call(buttons, function (button) {
      button.classList.toggle("active", button.dataset.source === state.source);
    });
  }

  function saveSettings() {
    if (bridge) {
      bridge.saveSettings(state.player, state.source);
    }
  }

  /* ---------- 宿主回调 ---------- */
  window.XfmclNative = {
    onInit: function (json) {
      var data = parse(json) || {};
      state.configured = !!data.configured;
      state.player = data.player || "Player";
      state.source = data.source === "MIRROR" ? "MIRROR" : "OFFICIAL";

      $("playerName").value = state.player;
      $("gameDir").textContent = data.gameDir || "—";

      var pill = $("endpointPill");
      pill.textContent = state.configured ? "端点已配置" : "未配置下载端点";
      pill.classList.toggle("warn", !state.configured);

      updateSourceButtons();
      setBusy(false);

      if (!state.configured) {
        setStatus("尚未配置下载端点");
        log("请在 assets/endpoints.json 中填写 manifestUrl 与 mirrorRoot。");
        return;
      }

      setStatus("正在读取版本清单…");
      bridge.loadVersions(false);
    },

    onVersions: function (json) {
      var list = parse(json);
      state.versions = Array.isArray(list) ? list : [];
      selectDefault();
      renderVersions();
      renderSelection();
      setBusy(false);
      setStatus("共 " + state.versions.length + " 个版本");
    },

    onProgress: function (json) {
      var progress = parse(json) || {};
      setProgress(progress.completed || 0, progress.total || 0, progress.bytes || 0);
      setStatus("正在下载" + (progress.stage ? progress.stage : "") + "…");
    },

    onLog: function (text) {
      log(text);
    },

    onDone: function (ok, message) {
      setBusy(false);
      setStatus(message || (ok ? "完成" : "已取消"));
      if (message) {
        log(message);
      }

      if (ok && bridge) {
        bridge.loadVersions(false);
      }
    },

    onError: function (message) {
      setBusy(false);
      setStatus("操作失败");
      log("错误：" + (message || "未知错误"));
      toast(message || "操作失败");
    }
  };

  /* ---------- 事件 ---------- */
  $("search").addEventListener("input", function (event) {
    state.filter = event.target.value.trim();
    renderVersions();
  });

  $("refreshButton").addEventListener("click", function () {
    if (!state.configured) {
      toast("请先配置下载端点");
      return;
    }

    setBusy(true);
    setStatus("正在刷新版本清单…");
    bridge.loadVersions(true);
  });

  $("downloadButton").addEventListener("click", function () {
    if (!state.selected) {
      toast("请先选择一个版本");
      return;
    }

    if (!state.configured) {
      toast("请先配置下载端点");
      return;
    }

    state.player = ($("playerName").value || "").trim();
    saveSettings();
    setBusy(true);
    $("progressBar").style.width = "0%";
    $("progressText").textContent = "";
    setStatus("正在准备 " + state.selected + "…");
    bridge.download(state.selected);
  });

  $("cancelButton").addEventListener("click", function () {
    if (bridge) {
      bridge.cancel();
      setStatus("正在取消…");
    }
  });

  $("openDirButton").addEventListener("click", function () {
    if (bridge) {
      bridge.openDirectory();
    }
  });

  $("playerName").addEventListener("change", function () {
    state.player = ($("playerName").value || "").trim();
    saveSettings();
  });

  document.querySelectorAll("#sourceSwitch button").forEach(function (button) {
    button.addEventListener("click", function () {
      state.source = button.dataset.source === "MIRROR" ? "MIRROR" : "OFFICIAL";
      updateSourceButtons();
      saveSettings();
    });
  });

  /* ---------- 启动 ---------- */
  if (!bridge) {
    setStatus("仅 Android 宿主内可用");
    log("当前页面未连接到 Android 宿主，下载功能不可用。");
    $("downloadButton").disabled = true;
    $("refreshButton").disabled = true;
    return;
  }

  setBusy(false);
  bridge.init();
})();
