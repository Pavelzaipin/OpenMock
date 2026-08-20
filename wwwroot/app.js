const mocksEl = document.getElementById('mocks');
const form = document.getElementById('mockForm');
const editorTitle = document.getElementById('editorTitle');
const saveBtn = document.getElementById('saveBtn');
const cancelEditBtn = document.getElementById('cancelEdit');
const connStatus = document.getElementById('connStatus');
const MAX_LOG_ENTRIES = 100;

let editingId = null;
let socket = null;

function esc(s) {
    return String(s).replace(/[&<>"']/g, c => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[c]));
}

async function loadMocks() {
    const mocks = await (await fetch('/api/mocks')).json();
    mocksEl.innerHTML = '';
    for (const mock of mocks) mocksEl.appendChild(renderMock(mock));
}

function renderMock(mock) {
    const card = document.createElement('section');
    card.className = 'card';
    card.dataset.id = mock.id;
    card.innerHTML = `
        <div class="mock-head">
            <span class="method ${esc(mock.method)}">${esc(mock.method)}</span>
            <code>${esc(mock.path)}</code>
            <span class="status-tag">→ ${mock.statusCode} ${esc(mock.contentType)}</span>
            <span class="spacer"></span>
            <button class="edit">Изменить</button>
            <button class="del">Удалить</button>
            <button class="clear">Очистить лог</button>
        </div>
        <ul class="log" id="log-${mock.id}"></ul>`;
    card.querySelector('.edit').addEventListener('click', () => startEdit(mock));
    card.querySelector('.del').addEventListener('click', async () => {
        if (!confirm(`Удалить мок ${mock.method} ${mock.path}?`)) return;
        await fetch(`/api/mocks/${mock.id}`, { method: 'DELETE' });
        if (editingId === mock.id) stopEdit();
        await loadMocks();
    });
    card.querySelector('.clear').addEventListener('click', () => {
        card.querySelector('.log').innerHTML = '';
    });
    return card;
}

function startEdit(mock) {
    editingId = mock.id;
    document.getElementById('fMethod').value = mock.method;
    document.getElementById('fPath').value = mock.path;
    document.getElementById('fStatus').value = mock.statusCode;
    document.getElementById('fContentType').value = mock.contentType;
    document.getElementById('fBody').value = mock.body;
    editorTitle.textContent = 'Редактирование мока';
    saveBtn.textContent = 'Сохранить';
    cancelEditBtn.hidden = false;
    window.scrollTo({ top: 0, behavior: 'smooth' });
}

function stopEdit() {
    editingId = null;
    form.reset();
    document.getElementById('fStatus').value = 200;
    document.getElementById('fContentType').value = 'application/json';
    editorTitle.textContent = 'Новый мок';
    saveBtn.textContent = 'Добавить';
    cancelEditBtn.hidden = true;
}

cancelEditBtn.addEventListener('click', stopEdit);

form.addEventListener('submit', async e => {
    e.preventDefault();
    const mock = {
        method: document.getElementById('fMethod').value,
        path: document.getElementById('fPath').value,
        statusCode: Number(document.getElementById('fStatus').value) || 200,
        contentType: document.getElementById('fContentType').value || 'application/json',
        body: document.getElementById('fBody').value
    };
    if (editingId) {
        await fetch(`/api/mocks/${editingId}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(mock)
        });
    } else {
        await fetch('/api/mocks', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(mock)
        });
    }
    stopEdit();
    await loadMocks();
});

document.querySelector('#unmatchedCard .clear').addEventListener('click', () => {
    document.getElementById('log-unmatched').innerHTML = '';
});

function appendHit(hit) {
    const logId = hit.mockId ? `log-${hit.mockId}` : 'log-unmatched';
    const log = document.getElementById(logId);
    if (!log) return; // hit for a mock deleted from the screen

    const time = new Date(hit.timestamp).toLocaleTimeString('ru-RU');
    const headers = Object.entries(hit.headers).map(([k, v]) => `${k}: ${v}`).join('\n');
    const li = document.createElement('li');
    li.innerHTML = `
        <details>
            <summary><time>${time}</time>${esc(hit.method)} ${esc(hit.path)}</summary>
            <pre>${esc(headers)}${hit.body ? '\n\n' + esc(hit.body) : ''}</pre>
        </details>`;
    log.prepend(li);
    while (log.children.length > MAX_LOG_ENTRIES) log.lastElementChild.remove();
}

function connect() {
    const proto = location.protocol === 'https:' ? 'wss' : 'ws';
    socket = new WebSocket(`${proto}://${location.host}/ws`);
    socket.onopen = () => {
        connStatus.textContent = 'в сети';
        connStatus.className = 'conn on';
    };
    socket.onclose = () => {
        connStatus.textContent = 'переподключение…';
        connStatus.className = 'conn off';
        setTimeout(connect, 2000);
    };
    socket.onmessage = e => appendHit(JSON.parse(e.data));
}

loadMocks();
connect();
