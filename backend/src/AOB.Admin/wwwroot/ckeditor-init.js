// Blazor JS interop wrapper for CKEditor 5 Super-Build (CDN).
// The super-build ships with every open-source plugin (fonts, colours, alignment,
// source editing, tables, etc.) so the UX is close to WordPress' block editor.

const CKE_VERSION = "41.4.2";
const CKE_URL = `https://cdn.ckeditor.com/ckeditor5/${CKE_VERSION}/super-build/ckeditor.js`;
const instances = new Map();

function loadCkeditor() {
    if (window.CKEDITOR && window.CKEDITOR.ClassicEditor) return Promise.resolve();
    if (window.__ckeditorLoading) return window.__ckeditorLoading;
    window.__ckeditorLoading = new Promise((resolve, reject) => {
        const s = document.createElement("script");
        s.src = CKE_URL;
        s.onload = resolve;
        s.onerror = () => reject(new Error("Failed to load CKEditor super-build from " + CKE_URL));
        document.head.appendChild(s);
    });
    return window.__ckeditorLoading;
}

function siteSlugFromUrl(url) {
    try {
        const u = new URL(url, window.location.origin);
        return u.searchParams.get("site") || "aob";
    } catch { return "aob"; }
}

function formatBytes(b) {
    if (!b) return "";
    if (b < 1024) return `${b} B`;
    const kb = b / 1024;
    if (kb < 1024) return `${kb.toFixed(1)} KB`;
    return `${(kb / 1024).toFixed(1)} MB`;
}

// Insert plain text at the current editor selection.
function insertShortcode(editor, text) {
    editor.model.change(writer => {
        const range = editor.model.document.selection.getFirstRange();
        writer.insertText(text, range.start);
    });
}

// ------- Download picker modal -------
function ensureModalStyles() {
    if (document.getElementById("aob-dl-styles")) return;
    const css = `
        .aob-dl-backdrop { position: fixed; inset: 0; background: rgba(0,0,0,.45); z-index: 1080; display: flex; align-items: center; justify-content: center; }
        .aob-dl-modal { background: #fff; border-radius: 8px; width: min(720px, 92vw); max-height: 82vh; display: flex; flex-direction: column; box-shadow: 0 20px 60px rgba(0,0,0,.25); overflow: hidden; }
        .aob-dl-head { display: flex; align-items: center; padding: 12px 16px; border-bottom: 1px solid #e5e7eb; gap: 8px; }
        .aob-dl-head h5 { margin: 0; font-size: 1rem; flex: 1; }
        .aob-dl-close { background: none; border: 0; font-size: 1.4rem; line-height: 1; cursor: pointer; color: #6b7280; }
        .aob-dl-body { padding: 12px 16px; overflow-y: auto; }
        .aob-dl-search { width: 100%; padding: 8px 10px; border: 1px solid #d1d5db; border-radius: 6px; margin-bottom: 10px; }
        .aob-dl-list { display: flex; flex-direction: column; gap: 4px; }
        .aob-dl-item { display: flex; align-items: center; gap: 10px; padding: 8px 10px; border: 1px solid #e5e7eb; border-radius: 6px; cursor: pointer; background: #fff; }
        .aob-dl-item:hover { background: #f3f4f6; border-color: #9ca3af; }
        .aob-dl-item .aob-dl-title { flex: 1; font-weight: 500; }
        .aob-dl-item .aob-dl-meta { color: #6b7280; font-size: .85em; }
        .aob-dl-foot { border-top: 1px solid #e5e7eb; padding: 12px 16px; display: flex; align-items: center; gap: 10px; background: #f9fafb; }
        .aob-dl-upload { display: flex; align-items: center; gap: 8px; flex: 1; }
        .aob-dl-status { color: #6b7280; font-size: .85em; margin-left: auto; }
        .aob-dl-btn { padding: 6px 12px; border-radius: 6px; border: 1px solid #d1d5db; background: #fff; cursor: pointer; }
        .aob-dl-btn.primary { background: #2563eb; border-color: #2563eb; color: #fff; }
        .aob-dl-btn.primary:disabled { background: #93c5fd; border-color: #93c5fd; cursor: not-allowed; }
        .aob-dl-empty { padding: 20px; text-align: center; color: #6b7280; }
    `;
    const style = document.createElement("style");
    style.id = "aob-dl-styles";
    style.textContent = css;
    document.head.appendChild(style);
}

function openDownloadPicker(editor, siteSlug) {
    ensureModalStyles();

    const backdrop = document.createElement("div");
    backdrop.className = "aob-dl-backdrop";
    backdrop.innerHTML = `
        <div class="aob-dl-modal" role="dialog" aria-modal="true">
            <div class="aob-dl-head">
                <h5>Inserir download <span class="text-muted" style="font-weight:400">(${siteSlug})</span></h5>
                <button type="button" class="aob-dl-close" aria-label="Fechar">&times;</button>
            </div>
            <div class="aob-dl-body">
                <input type="search" class="aob-dl-search" placeholder="Pesquisar por titulo ou nome de ficheiro..." />
                <div class="aob-dl-list"><div class="aob-dl-empty">A carregar...</div></div>
            </div>
            <div class="aob-dl-foot">
                <label class="aob-dl-upload">
                    <span>Ou upload novo:</span>
                    <input type="file" class="aob-dl-file" accept=".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.zip,.rar,.7z,.txt,.csv,.odt,.ods" />
                </label>
                <button type="button" class="aob-dl-btn primary aob-dl-upload-btn" disabled>Carregar e inserir</button>
                <span class="aob-dl-status"></span>
            </div>
        </div>
    `;
    document.body.appendChild(backdrop);

    const searchEl = backdrop.querySelector(".aob-dl-search");
    const listEl = backdrop.querySelector(".aob-dl-list");
    const fileEl = backdrop.querySelector(".aob-dl-file");
    const uploadBtn = backdrop.querySelector(".aob-dl-upload-btn");
    const statusEl = backdrop.querySelector(".aob-dl-status");

    let searchTimer;

    const close = () => backdrop.remove();
    backdrop.querySelector(".aob-dl-close").addEventListener("click", close);
    backdrop.addEventListener("click", e => { if (e.target === backdrop) close(); });

    async function loadList(q) {
        listEl.innerHTML = `<div class="aob-dl-empty">A carregar...</div>`;
        try {
            const url = new URL("/admin/downloads-lookup", window.location.origin);
            url.searchParams.set("site", siteSlug);
            if (q) url.searchParams.set("q", q);
            const r = await fetch(url, { credentials: "same-origin" });
            if (!r.ok) throw new Error(`HTTP ${r.status}`);
            const rows = await r.json();
            if (!rows.length) {
                listEl.innerHTML = `<div class="aob-dl-empty">Nenhum download encontrado. Faz upload em baixo.</div>`;
                return;
            }
            listEl.innerHTML = "";
            for (const dl of rows) {
                const item = document.createElement("div");
                item.className = "aob-dl-item";
                item.innerHTML = `
                    <div class="aob-dl-title">${escapeHtml(dl.title)}${dl.published ? "" : " <em style='color:#dc2626'>(rascunho)</em>"}</div>
                    <div class="aob-dl-meta">${escapeHtml(dl.fileName || "")} &middot; ${formatBytes(dl.size)}</div>
                `;
                item.addEventListener("click", () => {
                    insertShortcode(editor, `{download id=${dl.id}}`);
                    close();
                });
                listEl.appendChild(item);
            }
        } catch (err) {
            listEl.innerHTML = `<div class="aob-dl-empty" style="color:#dc2626">Erro: ${escapeHtml(err.message)}</div>`;
        }
    }

    searchEl.addEventListener("input", () => {
        clearTimeout(searchTimer);
        searchTimer = setTimeout(() => loadList(searchEl.value.trim()), 250);
    });

    fileEl.addEventListener("change", () => {
        uploadBtn.disabled = !fileEl.files.length;
    });

    uploadBtn.addEventListener("click", async () => {
        if (!fileEl.files.length) return;
        uploadBtn.disabled = true;
        statusEl.textContent = "A enviar...";
        try {
            const fd = new FormData();
            fd.append("file", fileEl.files[0]);
            fd.append("site", siteSlug);
            fd.append("title", fileEl.files[0].name.replace(/\.[^.]+$/, ""));
            const r = await fetch("/admin/downloads-quick-create", { method: "POST", body: fd, credentials: "same-origin" });
            if (!r.ok) {
                const txt = await r.text();
                throw new Error(txt || `HTTP ${r.status}`);
            }
            const dl = await r.json();
            insertShortcode(editor, `{download id=${dl.id}}`);
            close();
        } catch (err) {
            statusEl.textContent = "Erro: " + err.message;
            uploadBtn.disabled = false;
        }
    });

    loadList("");
    setTimeout(() => searchEl.focus(), 50);
}

function escapeHtml(s) {
    return String(s ?? "").replace(/[&<>"']/g, c =>
        ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}

// Injects the "Inserir download" button just above the editor UI.
function attachDownloadButton(editor, uploadUrl) {
    const siteSlug = siteSlugFromUrl(uploadUrl);
    const editable = editor.ui.getEditableElement();
    if (!editable) return;
    const wrapper = editable.closest(".ck-editor") || editable.parentElement;
    if (!wrapper) return;
    const host = wrapper.parentElement;
    if (!host || host.querySelector(":scope > .aob-dl-trigger-bar")) return;

    const bar = document.createElement("div");
    bar.className = "aob-dl-trigger-bar";
    bar.style.cssText = "display:flex;gap:6px;padding:6px 0;align-items:center;";
    bar.innerHTML = `
        <button type="button" class="aob-dl-trigger btn btn-sm btn-outline-primary">
            📎 Inserir download
        </button>
        <span class="text-muted small">Insere um shortcode <code>{download id=…}</code> na posicao do cursor.</span>
    `;
    host.insertBefore(bar, wrapper);
    bar.querySelector(".aob-dl-trigger").addEventListener("click", () => openDownloadPicker(editor, siteSlug));
}

export async function attach(elementId, initialHtml, dotNetRef, uploadUrl) {
    try {
        await loadCkeditor();
    } catch (err) {
        console.error(err);
        return;
    }
    const el = document.getElementById(elementId);
    if (!el) return;
    if (instances.has(elementId)) {
        try { await instances.get(elementId).destroy(); } catch (e) {}
        instances.delete(elementId);
    }
    el.value = initialHtml || "";

    class UploadAdapter {
        constructor(loader) { this.loader = loader; }
        upload() {
            return this.loader.file.then(file => new Promise((resolve, reject) => {
                const data = new FormData();
                data.append("file", file);
                fetch(uploadUrl, { method: "POST", body: data, credentials: "same-origin" })
                    .then(r => r.ok ? r.json() : Promise.reject(r.statusText))
                    .then(json => resolve({ default: json.url }))
                    .catch(reject);
            }));
        }
        abort() {}
    }

    const ClassicEditor = window.CKEDITOR.ClassicEditor;

    const editor = await ClassicEditor.create(el, {
        licenseKey: "GPL",
        toolbar: {
            items: [
                "undo", "redo", "|",
                "heading", "style", "|",
                "bold", "italic", "underline", "strikethrough", "removeFormat", "|",
                "fontFamily", "fontSize", "fontColor", "fontBackgroundColor", "highlight", "|",
                "alignment", "|",
                "bulletedList", "numberedList", "todoList", "outdent", "indent", "|",
                "link", "blockQuote", "insertTable", "horizontalLine", "specialCharacters", "|",
                "imageUpload", "mediaEmbed", "|",
                "sourceEditing"
            ],
            shouldNotGroupWhenFull: true
        },
        heading: {
            options: [
                { model: "paragraph", title: "Paragrafo", class: "ck-heading_paragraph" },
                { model: "heading2", view: "h2", title: "Titulo H2", class: "ck-heading_heading2" },
                { model: "heading3", view: "h3", title: "Titulo H3", class: "ck-heading_heading3" },
                { model: "heading4", view: "h4", title: "Titulo H4", class: "ck-heading_heading4" }
            ]
        },
        image: {
            toolbar: [
                "imageStyle:inline", "imageStyle:block", "imageStyle:side", "|",
                "toggleImageCaption", "imageTextAlternative", "|",
                "linkImage", "|",
                "resizeImage"
            ],
            resizeUnit: "%",
            resizeOptions: [
                { name: "resizeImage:original", value: null, label: "Original" },
                { name: "resizeImage:25", value: "25", label: "25%" },
                { name: "resizeImage:50", value: "50", label: "50%" },
                { name: "resizeImage:75", value: "75", label: "75%" }
            ]
        },
        table: {
            contentToolbar: [
                "tableColumn", "tableRow", "mergeTableCells",
                "tableProperties", "tableCellProperties"
            ]
        },
        link: {
            addTargetToExternalLinks: true,
            defaultProtocol: "https://"
        },
        removePlugins: [
            "AIAssistant", "CKBox", "CKFinder", "EasyImage",
            "RealTimeCollaborativeComments", "RealTimeCollaborativeTrackChanges",
            "RealTimeCollaborativeRevisionHistory", "PresenceList", "Comments",
            "TrackChanges", "TrackChangesData", "RevisionHistory",
            "Pagination", "WProofreader", "MathType",
            "SlashCommand", "Template", "DocumentOutline", "FormatPainter",
            "TableOfContents", "PasteFromOfficeEnhanced", "CaseChange",
            "ExportPdf", "ExportWord", "MultiLevelList", "ImportWord",
            "MergeFields", "Uploadcare", "UploadcareImageEdit"
        ],
        language: "pt"
    });

    editor.plugins.get("FileRepository").createUploadAdapter = loader => new UploadAdapter(loader);
    editor.model.document.on("change:data", () => {
        dotNetRef.invokeMethodAsync("OnEditorContentChanged", editor.getData());
    });
    instances.set(elementId, editor);

    attachDownloadButton(editor, uploadUrl);
}

export async function detach(elementId) {
    if (instances.has(elementId)) {
        try { await instances.get(elementId).destroy(); } catch (e) {}
        instances.delete(elementId);
    }
}

export function setContent(elementId, html) {
    const editor = instances.get(elementId);
    if (editor) editor.setData(html || "");
}
