/* ==========================================================================
   文件管理器交互：资源管理器式多选、右键菜单、预览面板、分片上传
   ========================================================================== */
(function () {
    'use strict';

    var app = document.getElementById('fmApp');
    if (!app) return;

    var cfg = {
        folderId: app.dataset.folder || '',
        index: app.dataset.urlIndex,
        uploadInit: app.dataset.urlUploadInit,
        uploadChunk: app.dataset.urlUploadChunk,
        uploadComplete: app.dataset.urlUploadComplete,
        uploadAbort: app.dataset.urlUploadAbort,
        share: app.dataset.urlShare,
        markdown: app.dataset.urlMarkdown,
        renameFile: app.dataset.urlRenameFile,
        renameFolder: app.dataset.urlRenameFolder,
        moveFile: app.dataset.urlMoveFile,
        moveFolder: app.dataset.urlMoveFolder,
        deleteFile: app.dataset.urlDeleteFile,
        deleteFolder: app.dataset.urlDeleteFolder
    };

    var tokenEl = document.querySelector('#afForm input[name="__RequestVerificationToken"]');
    var afToken = tokenEl ? tokenEl.value : '';

    var list = document.getElementById('fmList');
    var checkAll = document.getElementById('checkAll');
    var btnShare = document.getElementById('btnShare');
    var btnMarkdown = document.getElementById('btnMarkdown');
    var btnDownload = document.getElementById('btnDownload');
    var btnMove = document.getElementById('btnMove');
    var btnDelete = document.getElementById('btnDelete');
    var menu = document.getElementById('fmMenu');
    var statusSel = document.getElementById('fmSelText');

    var selected = new Set();
    var anchorRow = null;

    // ---------------- 基础工具 ----------------
    function rows() {
        return Array.prototype.slice.call(list.querySelectorAll('.fm-row'));
    }

    function fmtSize(bytes) {
        var n = Number(bytes) || 0;
        if (n >= 1073741824) return (n / 1073741824).toFixed(2) + ' GB';
        if (n >= 1048576) return (n / 1048576).toFixed(1) + ' MB';
        if (n >= 1024) return (n / 1024).toFixed(0) + ' KB';
        return n + ' B';
    }

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function postForm(url, data) {
        var fd = new FormData();
        if (afToken) fd.append('__RequestVerificationToken', afToken);
        Object.keys(data || {}).forEach(function (k) {
            var v = data[k];
            if (v === undefined || v === null) return;
            if (Array.isArray(v)) {
                v.forEach(function (item) { fd.append(k, item); });
            } else {
                fd.append(k, v);
            }
        });
        return fetch(url, {
            method: 'POST',
            body: fd,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        }).then(function (res) {
            return res.text().then(function (text) {
                try {
                    return JSON.parse(text);
                } catch (e) {
                    throw new Error('服务器返回异常（HTTP ' + res.status + '）');
                }
            });
        });
    }

    function xhrPost(url, fd, onProgress) {
        return new Promise(function (resolve, reject) {
            var xhr = new XMLHttpRequest();
            xhr.open('POST', url, true);
            xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
            xhr.upload.onprogress = function (e) {
                if (e.lengthComputable && onProgress) onProgress(e.loaded);
            };
            xhr.onload = function () {
                try {
                    resolve(JSON.parse(xhr.responseText));
                } catch (e) {
                    reject(new Error('服务器返回异常（HTTP ' + xhr.status + '）'));
                }
            };
            xhr.onerror = function () { reject(new Error('网络错误，上传中断')); };
            xhr.onabort = function () { reject({ canceled: true }); };
            if (currentXhrRef) currentXhrRef.xhr = xhr;
            xhr.send(fd);
        });
    }

    var currentXhrRef = null;

    function postAndGo(action, fields) {
        var form = document.getElementById('opForm');
        form.action = action;
        form.innerHTML = '';
        var t = document.createElement('input');
        t.type = 'hidden';
        t.name = '__RequestVerificationToken';
        t.value = afToken;
        form.appendChild(t);

        Object.keys(fields).forEach(function (name) {
            var values = Array.isArray(fields[name]) ? fields[name] : [fields[name]];
            values.forEach(function (v) {
                var i = document.createElement('input');
                i.type = 'hidden';
                i.name = name;
                i.value = v;
                form.appendChild(i);
            });
        });
        form.submit();
    }

    // ---------------- 选择逻辑 ----------------
    function setSelected(row, on) {
        if (!row) return;
        if (on) selected.add(row); else selected.delete(row);
        row.classList.toggle('is-selected', on);
        var cb = row.querySelector('.fm-row-check');
        if (cb) cb.checked = on;
    }

    function clearSelection() {
        selected.forEach(function (r) { setSelected(r, false); });
        selected.clear();
        refresh();
    }

    function selectAll() {
        rows().forEach(function (r) { setSelected(r, true); });
        refresh();
    }

    function selectOnly(row) {
        selected.forEach(function (r) { setSelected(r, false); });
        selected.clear();
        setSelected(row, true);
        refresh();
    }

    function selectRange(from, to) {
        var all = rows();
        var i = all.indexOf(from);
        var j = all.indexOf(to);
        if (i < 0 || j < 0) return;
        var start = Math.min(i, j);
        var end = Math.max(i, j);
        for (var k = start; k <= end; k++) setSelected(all[k], true);
        refresh();
    }

    function singleRow() {
        return selected.size === 1 ? selected.values().next().value : null;
    }

    function refresh() {
        var count = selected.size;
        rows().forEach(function (r) { r.setAttribute('aria-selected', r.classList.contains('is-selected') ? 'true' : 'false'); });

        statusSel.textContent = count === 0 ? '未选择' : '已选择 ' + count + ' 项';
        if (checkAll) {
            var total = rows().length;
            checkAll.checked = total > 0 && count === total;
            checkAll.indeterminate = count > 0 && count < total;
        }

        var one = singleRow();
        btnShare.disabled = count !== 1;
        btnDownload.disabled = !(count === 1 && one && one.dataset.kind === 'file');
        if (btnMarkdown) btnMarkdown.disabled = count === 0;
        btnMove.disabled = count === 0;
        btnDelete.disabled = count === 0;

        renderPreview(one, count);
    }

    // ---------------- 预览面板 ----------------
    var pvEmpty = document.getElementById('pvEmpty');
    var pvBody = document.getElementById('pvBody');
    var pvThumb = document.getElementById('pvThumb');
    var pvTitle = document.getElementById('pvTitle');
    var pvType = document.getElementById('pvType');
    var pvSize = document.getElementById('pvSize');
    var pvDate = document.getElementById('pvDate');
    var pvScope = document.getElementById('pvScope');
    var pvKey = document.getElementById('pvKey');
    var pvOpen = document.getElementById('pvOpen');
    var pvDownload = document.getElementById('pvDownload');
    var pvShare = document.getElementById('pvShare');
    var pvRename = document.getElementById('pvRename');
    var pvDelete = document.getElementById('pvDelete');

    function renderPreview(row, count) {
        if (!row || count !== 1) {
            pvEmpty.hidden = false;
            pvBody.hidden = true;
            pvEmpty.querySelector('span').textContent = count > 1
                ? '已选择 ' + count + ' 项，可批量移动、删除或生成 Markdown'
                : '选择一个文件查看详细信息';
            return;
        }

        var d = row.dataset;
        pvEmpty.hidden = true;
        pvBody.hidden = false;
        pvTitle.textContent = d.name;
        pvType.textContent = d.type;
        pvSize.textContent = d.kind === 'folder' ? (d.count && d.count !== '0' ? d.count + ' 项' : '空文件夹') : d.sizeText;
        pvDate.textContent = d.date;
        pvKey.textContent = d.kind === 'file' ? (d.key || '—') : '—';

        if (d.kind === 'folder') {
            pvThumb.innerHTML = '<svg class="ico" aria-hidden="true"><use href="#i-folder" /></svg>';
            pvDownload.disabled = true;
        } else if (d.image === '1') {
            pvThumb.innerHTML = '<img src="' + esc(d.url) + '" alt="" />';
            pvDownload.disabled = false;
        } else {
            pvThumb.innerHTML = '<svg class="ico" aria-hidden="true"><use href="#i-file" /></svg>';
            pvDownload.disabled = false;
        }

        if (d.kind === 'folder') {
            pvScope.textContent = d.share ? '已共享（文件夹）' : '私有';
        } else {
            pvScope.textContent = d.public === '1' ? '公开素材' : (d.share ? '已共享' : '私有');
        }

        pvOpen.disabled = false;
        pvShare.disabled = false;
        pvRename.disabled = false;
    }

    // ---------------- 行交互 ----------------
    rows().forEach(function (row) {
        row.addEventListener('click', function (e) {
            if (e.target.closest('.fm-check')) return;
            if (e.ctrlKey || e.metaKey) {
                setSelected(row, !row.classList.contains('is-selected'));
            } else if (e.shiftKey && anchorRow) {
                selectRange(anchorRow, row);
            } else {
                selectOnly(row);
            }
            anchorRow = row;
            refresh();
        });

        row.addEventListener('dblclick', function (e) {
            if (e.target.closest('.fm-check')) return;
            openRow(row);
        });

        row.addEventListener('contextmenu', function (e) {
            e.preventDefault();
            if (!row.classList.contains('is-selected')) selectOnly(row);
            showMenu(e.clientX, e.clientY);
        });

        var cb = row.querySelector('.fm-row-check');
        if (cb) {
            cb.addEventListener('change', function () {
                setSelected(row, cb.checked);
                anchorRow = row;
                refresh();
            });
        }
    });

    if (checkAll) {
        checkAll.addEventListener('change', function () {
            if (checkAll.checked) selectAll(); else clearSelection();
        });
    }

    // 点击空白处取消选择
    list.addEventListener('mousedown', function (e) {
        if (e.target === list || e.target.classList.contains('fm-empty')) clearSelection();
    });

    // ---------------- 打开 / 下载 ----------------
    function openRow(row) {
        if (!row) return;
        if (row.dataset.kind === 'folder') {
            if (row.dataset.href) window.location.href = row.dataset.href;
            return;
        }
        if (row.dataset.image === '1') {
            var modalEl = document.getElementById('imageModal');
            document.getElementById('imgTitle').textContent = row.dataset.name;
            document.getElementById('imgPreview').src = row.dataset.url;
            bootstrap.Modal.getOrCreateInstance(modalEl).show();
        } else {
            downloadRow(row);
        }
    }

    function downloadRow(row) {
        if (!row || row.dataset.kind !== 'file') return;
        window.location.href = row.dataset.download;
    }

    pvOpen.addEventListener('click', function () { openRow(singleRow()); });
    pvDownload.addEventListener('click', function () { downloadRow(singleRow()); });
    pvShare.addEventListener('click', function () { openShare(singleRow()); });
    pvRename.addEventListener('click', function () { openRename(singleRow()); });
    pvDelete.addEventListener('click', function () { deleteRows(Array.from(selected)); });

    btnDownload.addEventListener('click', function () { downloadRow(singleRow()); });
    btnShare.addEventListener('click', function () { openShare(singleRow()); });
    btnMarkdown && btnMarkdown.addEventListener('click', function () { openMarkdown(); });
    btnMove.addEventListener('click', function () { openMove(singleRow()); });
    btnDelete.addEventListener('click', function () { deleteRows(Array.from(selected)); });

    // ---------------- 右键菜单 ----------------
    function setMenuState(act, disabled) {
        var el = menu.querySelector('[data-act="' + act + '"]');
        if (el) el.disabled = disabled;
    }

    function showMenu(x, y) {
        var items = Array.from(selected);
        if (!items.length) return;
        var one = items.length === 1 ? items[0] : null;
        var isFile = !!one && one.dataset.kind === 'file';
        var hasShare = !!one && !!one.dataset.share;

        setMenuState('open', !one);
        setMenuState('download', !isFile);
        setMenuState('share', !one);
        setMenuState('copy', !hasShare);
        setMenuState('markdown', items.length === 0);
        setMenuState('rename', !one);
        setMenuState('move', items.length === 0);
        setMenuState('delete', items.length === 0);
        menu.hidden = false;

        var rect = menu.getBoundingClientRect();
        var left = Math.min(x, window.innerWidth - rect.width - 8);
        var top = Math.min(y, window.innerHeight - rect.height - 8);
        menu.style.left = Math.max(8, left) + 'px';
        menu.style.top = Math.max(8, top) + 'px';
    }

    function hideMenu() { menu.hidden = true; }

    document.addEventListener('click', function (e) {
        if (!menu.hidden && !menu.contains(e.target)) hideMenu();
    });
    document.addEventListener('scroll', hideMenu, true);
    window.addEventListener('resize', hideMenu);

    menu.addEventListener('click', function (e) {
        var btn = e.target.closest('button[data-act]');
        if (!btn || btn.disabled) return;
        var act = btn.dataset.act;
        var items = Array.from(selected);
        var one = items.length === 1 ? items[0] : null;
        hideMenu();
        if (!items.length) return;

        if (act === 'markdown') { openMarkdown(); return; }
        if (!one) return;

        if (act === 'open') openRow(one);
        else if (act === 'download') downloadRow(one);
        else if (act === 'share') openShare(one);
        else if (act === 'copy') copyText(location.origin + '/share/' + one.dataset.share);
        else if (act === 'rename') openRename(one);
        else if (act === 'move') openMove(one);
        else if (act === 'delete') deleteRows(items);
    });

    // ---------------- 复制 ----------------
    function copyText(text) {
        function fallback() {
            var t = document.createElement('textarea');
            t.value = text;
            t.style.position = 'fixed';
            t.style.opacity = '0';
            document.body.appendChild(t);
            t.select();
            try { document.execCommand('copy'); } catch (e) { }
            document.body.removeChild(t);
        }
        if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(text).catch(fallback);
        } else {
            fallback();
        }
    }

    // ---------------- 重命名 ----------------
    var renameForm = document.getElementById('renameForm');
    var renameModal = bootstrap.Modal.getOrCreateInstance(document.getElementById('renameModal'));

    function openRename(row) {
        if (!row) return;
        var d = row.dataset;
        document.getElementById('rnId').value = d.id;
        document.getElementById('rnTitle').textContent = d.kind === 'folder' ? '重命名文件夹' : '重命名文件';

        var fileWrap = document.getElementById('rnFileWrap');
        var folderWrap = document.getElementById('rnFolderWrap');
        var fileName = document.getElementById('rnFileName');
        var folderName = document.getElementById('rnFolderName');

        if (d.kind === 'folder') {
            renameForm.action = cfg.renameFolder;
            fileWrap.hidden = true;
            folderWrap.hidden = false;
            fileName.required = false;
            folderName.required = true;
            folderName.value = d.name;
            document.getElementById('rnDesc').value = d.desc || '';
            document.getElementById('rnSort').value = d.sort || 0;
        } else {
            renameForm.action = cfg.renameFile;
            fileWrap.hidden = false;
            folderWrap.hidden = true;
            folderName.required = false;
            fileName.required = true;
            fileName.value = d.name;
            document.getElementById('rnAlt').value = '';
        }
        renameModal.show();
        setTimeout(function () {
            (d.kind === 'folder' ? folderName : fileName).focus();
            (d.kind === 'folder' ? folderName : fileName).select();
        }, 250);
    }

    // ---------------- 移动 ----------------
    var moveForm = document.getElementById('moveForm');
    var moveModal = bootstrap.Modal.getOrCreateInstance(document.getElementById('moveModal'));
    var mvIds = document.getElementById('mvIds');

    function openMove(row) {
        var targets = Array.from(selected).filter(function (r) { return r.dataset.kind === 'file'; });
        if (!targets.length && row && row.dataset.kind === 'folder') targets = [row];
        if (!targets.length) return;

        mvIds.innerHTML = '';
        document.getElementById('mvId').value = '';

        if (targets.length === 1 && targets[0].dataset.kind === 'folder') {
            moveForm.action = cfg.moveFolder;
            document.getElementById('mvId').value = targets[0].dataset.id;
            document.getElementById('mvHint').textContent = '不能移动到自身或其子目录。';
        } else {
            moveForm.action = cfg.moveFile;
            targets.forEach(function (r) {
                var i = document.createElement('input');
                i.type = 'hidden';
                i.name = 'ids';
                i.value = r.dataset.id;
                mvIds.appendChild(i);
            });
            document.getElementById('mvHint').textContent = '将移动 ' + targets.length + ' 个文件。';
        }
        moveModal.show();
    }

    // ---------------- 删除 ----------------
    function deleteRows(items) {
        if (!items.length) return;
        var folders = items.filter(function (r) { return r.dataset.kind === 'folder'; });
        if (folders.length) {
            if (items.length > 1) {
                alert('文件夹需要单独删除，请只选择一个文件夹。');
                return;
            }
            if (!window.confirm('删除文件夹会同时删除其中所有文件、子目录和分享记录，确定继续？')) return;
            postAndGo(cfg.deleteFolder, { id: folders[0].dataset.id });
            return;
        }

        if (!window.confirm('确定删除选中的 ' + items.length + ' 个文件？此操作不可撤销。')) return;
        postAndGo(cfg.deleteFile, { ids: items.map(function (r) { return r.dataset.id; }) });
    }

    // ---------------- 分享 ----------------
    var shareForm = document.getElementById('shareForm');
    var shareModal = bootstrap.Modal.getOrCreateInstance(document.getElementById('shareModal'));
    var shareRow = null;
    var shTargetEl = document.getElementById('shTarget');

    function resetShareUi() {
        document.getElementById('shFormWrap').hidden = false;
        document.getElementById('shResult').classList.remove('show');
        document.getElementById('shSubmit').hidden = false;
        document.getElementById('shSubmit').disabled = false;
        document.getElementById('shSubmit').textContent = '生成链接';
        document.getElementById('shError').hidden = true;
    }

    function openShare(row) {
        if (!row) return;
        shareRow = row;
        var d = row.dataset;
        document.getElementById('shMedia').value = d.kind === 'file' ? d.id : '0';
        document.getElementById('shFolder').value = d.kind === 'folder' ? d.id : '0';
        shTargetEl.textContent = (d.kind === 'folder' ? '文件夹：' : '文件：') + d.name;
        resetShareUi();
        shareModal.show();
    }

    shareForm.addEventListener('submit', function (e) {
        e.preventDefault();
        var btn = document.getElementById('shSubmit');
        var err = document.getElementById('shError');
        btn.disabled = true;
        btn.textContent = '生成中…';
        err.hidden = true;

        var fd = new FormData(shareForm);
        fetch(shareForm.action, {
            method: 'POST',
            body: fd,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        }).then(function (res) { return res.json(); }).then(function (data) {
            btn.disabled = false;
            if (!data.ok) {
                err.textContent = data.error || '生成失败';
                err.hidden = false;
                btn.textContent = '生成链接';
                return;
            }
            document.getElementById('shFormWrap').hidden = true;
            document.getElementById('shResult').classList.add('show');
            document.getElementById('shUrl').value = data.url;
            document.getElementById('shOpen').href = data.url;
            btn.hidden = true;

            if (shareRow) {
                shareRow.dataset.share = data.token;
                var cell = shareRow.querySelector('.fm-col-share');
                if (cell) {
                    cell.innerHTML = shareRow.dataset.kind === 'folder'
                        ? '<span class="fm-badge folder-shared">已共享</span>'
                        : '<span class="fm-badge shared">已共享</span>';
                }
                refresh();
            }
        }).catch(function (ex) {
            btn.disabled = false;
            btn.textContent = '生成链接';
            err.textContent = ex.message || '网络错误';
            err.hidden = false;
        });
    });

    document.getElementById('shCopy').addEventListener('click', function () {
        var input = document.getElementById('shUrl');
        copyText(input.value);
        this.textContent = '已复制';
        var self = this;
        setTimeout(function () { self.textContent = '复制'; }, 1400);
    });

    document.getElementById('shareModal').addEventListener('hidden.bs.modal', resetShareUi);

    // ---------------- 生成 Markdown ----------------
    // 页面可能来自旧版本（缺少弹窗），全部做空值保护，避免影响其它按钮
    var mdModalEl = document.getElementById('mdModal');
    var mdText = document.getElementById('mdText');
    var mdStat = document.getElementById('mdStat');
    var mdNote = document.getElementById('mdNote');
    var mdStyle = document.getElementById('mdStyle');
    var mdShare = document.getElementById('mdShare');
    var mdMeta = document.getElementById('mdMeta');
    var mdCopy = document.getElementById('mdCopy');
    var mdReady = !!(mdModalEl && mdText && mdStat && mdNote && mdStyle && mdShare && mdMeta && mdCopy);
    var mdModal = mdReady ? bootstrap.Modal.getOrCreateInstance(mdModalEl) : null;
    var mdSeq = 0;
    var pendingTokens = [];

    function openMarkdown() {
        if (!mdReady || !selected.size) return;
        resetMdUi();
        mdModal.show();
        buildMarkdown();
    }

    function resetMdUi() {
        if (!mdReady) return;
        mdText.value = '';
        mdNote.textContent = '';
        mdNote.hidden = true;
        mdStat.textContent = '正在生成…';
        mdStat.classList.remove('is-error');
        mdCopy.disabled = true;
        mdCopy.textContent = '复制 Markdown';
        pendingTokens = [];
    }

    function buildMarkdown() {
        if (!mdReady) return;
        var fileIds = [];
        var folderIds = [];
        selected.forEach(function (r) {
            if (r.dataset.kind === 'folder') folderIds.push(r.dataset.id);
            else fileIds.push(r.dataset.id);
        });

        var seq = ++mdSeq;
        mdStat.textContent = '正在生成…';
        mdStat.classList.remove('is-error');
        mdNote.hidden = true;
        mdCopy.disabled = true;

        postForm(cfg.markdown, {
            fileIds: fileIds,
            folderIds: folderIds,
            style: mdStyle.value,
            ensureAccess: mdShare.checked,
            includeMeta: mdMeta.checked
        }).then(function (data) {
            if (seq !== mdSeq) return;
            if (!data.ok) {
                mdStat.textContent = data.error || '生成失败';
                mdStat.classList.add('is-error');
                return;
            }

            mdText.value = data.markdown || '';
            mdText.scrollTop = 0;
            mdCopy.disabled = !mdText.value;

            var text = '已生成 ' + data.count + ' 个文件';
            if (data.shared > 0) text += '，新建 ' + data.shared + ' 条分享链接';
            mdStat.textContent = text;

            if (data.notes && data.notes.length) {
                mdNote.textContent = data.notes.slice(0, 4).join('；') + (data.notes.length > 4 ? ' 等' : '');
                mdNote.hidden = false;
            }

            pendingTokens = data.tokens || [];
        }).catch(function (ex) {
            if (seq !== mdSeq) return;
            mdStat.textContent = ex.message || '网络错误';
            mdStat.classList.add('is-error');
        });
    }

    if (mdReady) {
        [mdStyle, mdShare, mdMeta].forEach(function (el) {
            el.addEventListener('change', function () {
                if (mdModalEl.classList.contains('show')) buildMarkdown();
            });
        });

        mdCopy.addEventListener('click', function () {
            if (!mdText.value) return;
            copyText(mdText.value);
            mdCopy.textContent = '已复制';
            setTimeout(function () { mdCopy.textContent = '复制 Markdown'; }, 1400);
        });

        mdText.addEventListener('focus', function () { mdText.select(); });

        // 关闭时把新建的分享标记同步到列表行上
        mdModalEl.addEventListener('hidden.bs.modal', function () {
            if (!pendingTokens.length) return;
            pendingTokens.forEach(function (t) {
                var row = rows().filter(function (r) { return r.dataset.kind === 'file' && r.dataset.id === String(t.id); })[0];
                if (!row) return;
                row.dataset.share = t.token;
                var cell = row.querySelector('.fm-col-share');
                if (cell && row.dataset.public !== '1') {
                    cell.innerHTML = '<span class="fm-badge shared">已共享</span>';
                }
            });
            pendingTokens = [];
            refresh();
        });
    }

    // ---------------- 分片上传 ----------------
    var uploadModalEl = document.getElementById('uploadModal');
    var uploadModal = bootstrap.Modal.getOrCreateInstance(uploadModalEl);
    var upInput = document.getElementById('upInput');
    var upDrop = document.getElementById('upDrop');
    var upQueue = document.getElementById('upQueue');
    var upStart = document.getElementById('upStart');
    var upTotal = document.getElementById('upTotal');
    var upTotalFill = document.getElementById('upTotalFill');
    var upTotalText = document.getElementById('upTotalText');
    var upSpeed = document.getElementById('upSpeed');

    var queue = [];
    var uploading = false;
    var startTime = 0;
    var startBytes = 0;

    document.getElementById('btnUpload').addEventListener('click', function () {
        uploadModal.show();
    });

    upDrop.addEventListener('click', function () { upInput.click(); });
    upInput.addEventListener('change', function () {
        addFiles(upInput.files);
        upInput.value = '';
    });

    ['dragenter', 'dragover'].forEach(function (evt) {
        upDrop.addEventListener(evt, function (e) {
            e.preventDefault();
            upDrop.classList.add('is-over');
        });
    });
    ['dragleave', 'drop'].forEach(function (evt) {
        upDrop.addEventListener(evt, function (e) {
            e.preventDefault();
            upDrop.classList.remove('is-over');
        });
    });
    upDrop.addEventListener('drop', function (e) {
        e.stopPropagation();
        if (e.dataTransfer && e.dataTransfer.files) addFiles(e.dataTransfer.files);
    });

    // 整个窗口拖入也接收
    ['dragover', 'drop'].forEach(function (evt) {
        app.addEventListener(evt, function (e) {
            if (evt === 'drop' && e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files.length) {
                e.preventDefault();
                addFiles(e.dataTransfer.files);
                uploadModal.show();
            } else if (evt === 'dragover') {
                e.preventDefault();
            }
        });
    });

    function addFiles(files) {
        if (!files || !files.length) return;
        Array.prototype.forEach.call(files, function (file) {
            if (file.size === 0) return;
            var item = {
                file: file,
                status: 'pending',
                progress: 0,
                uploadId: null,
                canceled: false,
                el: null,
                fill: null,
                stateEl: null,
                xhr: null
            };
            buildQueueItem(item);
            queue.push(item);
        });
        syncQueue();
    }

    function buildQueueItem(item) {
        var el = document.createElement('div');
        el.className = 'fm-up-item';
        el.innerHTML =
            '<div class="fm-up-head">' +
            '<svg class="ico" aria-hidden="true"><use href="#i-file" /></svg>' +
            '<span class="fm-up-name"></span>' +
            '<span class="fm-up-size"></span>' +
            '<span class="fm-up-state">等待上传</span>' +
            '<button type="button" class="fm-up-remove" title="移除">✕</button>' +
            '</div>' +
            '<div class="fm-up-bar"><div class="fm-up-fill"></div></div>';
        el.querySelector('.fm-up-name').textContent = item.file.name;
        el.querySelector('.fm-up-name').title = item.file.name;
        el.querySelector('.fm-up-size').textContent = fmtSize(item.file.size);
        item.el = el;
        item.fill = el.querySelector('.fm-up-fill');
        item.stateEl = el.querySelector('.fm-up-state');

        el.querySelector('.fm-up-remove').addEventListener('click', function () {
            if (item.status === 'uploading') {
                item.canceled = true;
                if (item.xhr) item.xhr.abort();
                return;
            }
            el.remove();
            queue = queue.filter(function (q) { return q !== item; });
            syncQueue();
        });

        var empty = upQueue.querySelector('.fm-queue-empty');
        if (empty) empty.remove();
        upQueue.appendChild(el);
    }

    function setItemState(item, text, cls, progress) {
        if (item.stateEl) {
            item.stateEl.textContent = text;
            item.stateEl.className = 'fm-up-state' + (cls ? ' ' + cls : '');
        }
        if (typeof progress === 'number') {
            item.progress = Math.max(0, Math.min(1, progress));
            if (item.fill) item.fill.style.width = (item.progress * 100).toFixed(2) + '%';
        }
    }

    function syncQueue() {
        var pending = queue.filter(function (q) { return q.status === 'pending'; }).length;
        upStart.disabled = uploading || pending === 0;
        upStart.textContent = uploading ? '上传中…' : (pending > 0 ? '开始上传（' + pending + '）' : '开始上传');

        if (!queue.length) {
            upQueue.innerHTML = '<div class="fm-queue-empty">尚未选择文件</div>';
            upTotal.hidden = true;
            return;
        }

        upTotal.hidden = false;
        var done = queue.filter(function (q) { return q.status === 'done'; }).length;
        upTotalText.textContent = '已完成 ' + done + ' / ' + queue.length + ' 个文件';
        var totalProgress = queue.reduce(function (sum, q) { return sum + (q.status === 'done' ? 1 : q.progress); }, 0) / queue.length;
        upTotalFill.style.width = (totalProgress * 100).toFixed(2) + '%';
    }

    function updateSpeed(bytesDone) {
        var seconds = (Date.now() - startTime) / 1000;
        if (seconds <= 0) return;
        var speed = (bytesDone - startBytes) / seconds;
        upSpeed.textContent = speed > 0 ? fmtSize(speed) + '/s' : '';
    }

    upStart.addEventListener('click', function () {
        if (uploading) return;
        runQueue();
    });

    async function runQueue() {
        uploading = true;
        startTime = Date.now();
        startBytes = 0;
        upSpeed.textContent = '';
        syncQueue();

        var failed = 0;
        for (var i = 0; i < queue.length; i++) {
            var item = queue[i];
            if (item.status !== 'pending') continue;
            try {
                await uploadOne(item);
            } catch (ex) {
                item.status = ex && ex.canceled ? 'canceled' : 'failed';
                if (ex && ex.canceled) {
                    setItemState(item, '已取消', 'fail', 0);
                } else {
                    failed++;
                    setItemState(item, '失败', 'fail');
                    item.stateEl.title = (ex && ex.message) || '';
                }
                if (item.uploadId && ex && !ex.canceled) {
                    postForm(cfg.uploadAbort, { uploadId: item.uploadId }).catch(function () { });
                }
            }
            syncQueue();
        }

        uploading = false;
        syncQueue();

        if (failed === 0 && queue.some(function (q) { return q.status === 'done'; })) {
            upSpeed.textContent = '上传完成，正在刷新…';
            setTimeout(function () { window.location.reload(); }, 700);
        }
    }

    async function uploadOne(item) {
        setItemState(item, '准备中…', '', 0);
        var init = await postForm(cfg.uploadInit, {
            fileName: item.file.name,
            size: item.file.size
        });
        if (!init.ok) throw new Error(init.error || '初始化失败');

        item.uploadId = init.uploadId;
        var chunkSize = init.chunkSize || 8 * 1024 * 1024;
        var offset = 0;
        var index = 0;

        while (offset < item.file.size) {
            if (item.canceled) throw { canceled: true };
            var blob = item.file.slice(offset, offset + chunkSize);
            var fd = new FormData();
            fd.append('__RequestVerificationToken', afToken);
            fd.append('uploadId', init.uploadId);
            fd.append('index', index);
            fd.append('chunk', blob, 'chunk');

            var localOffset = offset;
            currentXhrRef = item;
            var res = await xhrPost(cfg.uploadChunk, fd, function (loaded) {
                var progress = (localOffset + loaded) / item.file.size;
                setItemState(item, (progress * 100).toFixed(1) + '%', '', progress);
                updateSpeed(localOffset + loaded);
            });
            item.xhr = null;

            if (!res.ok) throw new Error(res.error || '分片上传失败');
            offset += blob.size;
            index++;
        }

        setItemState(item, '校验中…', '', 0.99);
        var done = await postForm(cfg.uploadComplete, {
            uploadId: init.uploadId,
            fileName: item.file.name,
            size: item.file.size,
            totalChunks: index,
            folderId: cfg.folderId,
            contentType: item.file.type || ''
        });
        if (!done.ok) throw new Error(done.error || '合并失败');

        item.status = 'done';
        setItemState(item, '完成', 'done', 1);
        if (item.fill) item.fill.classList.add('done');
    }

    // ---------------- 快捷键 ----------------
    document.addEventListener('keydown', function (e) {
        var tag = (e.target.tagName || '').toLowerCase();
        if (tag === 'input' || tag === 'textarea' || tag === 'select' || e.target.isContentEditable) return;
        if (document.querySelector('.modal.show')) return;

        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'a') {
            e.preventDefault();
            selectAll();
            return;
        }
        if (e.key === 'Delete' || e.key === 'Backspace') {
            if (!selected.size) return;
            e.preventDefault();
            deleteRows(Array.from(selected));
            return;
        }
        if (e.key === 'F2') {
            var one = singleRow();
            if (one) { e.preventDefault(); openRename(one); }
            return;
        }
        if (e.key === 'Enter') {
            var cur = singleRow();
            if (cur) { e.preventDefault(); openRow(cur); }
            return;
        }
        if (e.key === 'Escape') {
            clearSelection();
            hideMenu();
        }
    });

    // ---------------- 初始化 ----------------
    // 宽屏默认显示详情面板
    if (window.innerWidth < 1500) app.classList.add('preview-off');
    document.getElementById('btnPane').addEventListener('click', function () {
        app.classList.toggle('preview-off');
    });

    // 图片预览关闭时清空 src，避免占内存
    document.getElementById('imageModal').addEventListener('hidden.bs.modal', function () {
        document.getElementById('imgPreview').removeAttribute('src');
    });

    refresh();
    syncQueue();
})();
