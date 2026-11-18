/* ============================================================
   CampusFlow · frontend ←→ REST API
   ============================================================ */

const API_BASE = '/api';
const TOKEN_KEY = 'campusflowToken';

const EQUIPMENT_TO_RU = {
    projector: 'Проектор',
    whiteboard: 'Интерактивная доска',
    computers: 'Компьютеры',
    audio: 'Акустическая система',
    video: 'Оборудование для видеоконференций'
};
const EQUIPMENT_FROM_RU = Object.fromEntries(
    Object.entries(EQUIPMENT_TO_RU).map(([k, v]) => [v.toLowerCase(), k])
);

const $ = id => document.getElementById(id);
const dateISO = d => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
const toMin = t => {
    const [h, m] = t.split(':').map(Number);
    return h * 60 + m;
};
const fromMin = m => `${String(Math.floor(m / 60)).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}`;
const today = new Date();
const tomorrow = new Date();
tomorrow.setDate(today.getDate() + 1);

let token = localStorage.getItem(TOKEN_KEY) || '';
let currentUserData = null;
let currentUser = 'guest';
let rooms = [];
let myBookings = [];
let allBookings = [];
let adminStats = null;
let selectedRoom = null;
let editingRoomId = null;
let freeSlots = [];

/* ---------- утилиты ---------- */
function escapeHtml(v) {
    return String(v ?? '').replace(/[&<>"']/g, ch => ({
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#39;'
    }[ch]));
}

function toast(s) {
    const n = document.createElement('div');
    n.className = 'toast';
    n.textContent = s;
    document.body.append(n);
    setTimeout(() => n.remove(), 2700);
}

function isGuest() {
    return currentUser === 'guest';
}

function isAdmin() {
    return !isGuest() && currentUserData?.role === 'admin';
}

function setToken(t) {
    token = t || '';
    if (t) localStorage.setItem(TOKEN_KEY, t);
    else localStorage.removeItem(TOKEN_KEY);
}

/* ---------- HTTP ---------- */
async function apiRequest(path, options = {}) {
    const hadToken = Boolean(token);
    const headers = {'Content-Type': 'application/json', ...(options.headers || {})};
    if (token) headers.Authorization = 'Bearer ' + token;

    const res = await fetch(API_BASE + path, {...options, headers});

    // 401 при запросе с уже сохранённым токеном означает, что сессия истекла.
    // 401 на /auth/login — просто неверные учётные данные, а не выход из аккаунта.
    if (res.status === 401 && hadToken && path !== '/auth/login') {
        setToken('');
        currentUserData = null;
        currentUser = 'guest';
        myBookings = [];
        allBookings = [];
        adminStats = null;
        handleUnauthorized();
    }

    return res;
}

async function apiJson(path, options = {}) {
    const res = await apiRequest(path, options);
    if (!res.ok) {
        let data = null;
        try {
            data = await res.json();
        } catch { /* ignore */
        }
        const err = new Error(data?.message || `HTTP ${res.status}`);
        err.code = data?.code;
        err.status = res.status;
        throw err;
    }
    if (res.status === 204) return null;
    const text = await res.text();
    return text ? JSON.parse(text) : null;
}

function apiErrorMessage(err) {
    const map = {
        VALIDATION_ERROR: 'Проверьте введённые данные',
        UNAUTHORIZED: 'Требуется авторизация',
        FORBIDDEN: 'Недостаточно прав',
        ROOM_NOT_FOUND: 'Аудитория не найдена',
        BOOKING_NOT_FOUND: 'Бронирование не найдено',
        BOOKING_CONFLICT: 'Аудитория уже забронирована на это время',
        ALREADY_CANCELLED: 'Бронирование уже отменено',
        ROOM_IN_USE: 'Нельзя удалить аудиторию: с ней связаны бронирования'
    };
    if (err?.code && map[err.code]) return map[err.code];
    return err?.message || 'Произошла ошибка';
}

function handleUnauthorized() {
    // Обновляем интерфейс целиком, чтобы после 401 не оставались данные
    // предыдущего пользователя и закрытые админские разделы.
    if ($('modal')) $('modal').classList.add('hidden');
    if ($('authScreen')) $('authScreen').classList.add('hidden');

    setUser();

    const currentPage = document.querySelector('.page.active');
    if (currentPage && currentPage.id !== 'overview') {
        document.querySelectorAll('.page').forEach(page => page.classList.remove('active'));
        $('overview').classList.add('active');
        document.querySelectorAll('.nav button').forEach(button => {
            button.classList.toggle('active', button.dataset.page === 'overview');
        });
        $('crumb').textContent = 'Обзор';
    }

    if ($('statsResult')) {
        $('statsResult').innerHTML = '<div class="empty">Войдите под учётной записью администратора, чтобы просмотреть статистику.</div>';
    }
    renderAll();
    toast('Сессия истекла. Войдите снова.');
}

/* ---------- нормализация ---------- */
function extractList(data) {
    if (Array.isArray(data)) return data;
    if (data && Array.isArray(data.items)) return data.items;
    if (data && Array.isArray(data.data)) return data.data;
    if (data && Array.isArray(data.rooms)) return data.rooms;
    return [];
}

function deriveRoomDesc(r) {
    if (r.description) return r.description;
    if (r.desc) return r.desc;
    const eq = (r.equipment || []).map(x => String(x).toLowerCase());
    if (eq.includes('computers') || eq.includes('компьютеры')) return 'Компьютерный класс';
    if ((r.capacity || 0) >= 50) return 'Лекционная аудитория';
    if ((r.capacity || 0) <= 15) return 'Малая аудитория';
    return 'Учебная аудитория';
}

function normalizeRoom(r) {
    return {
        id: r.id, name: r.name, building: r.building,
        floor: r.floor, capacity: r.capacity,
        equipment: (r.equipment || []).map(c => EQUIPMENT_TO_RU[c] || c),
        desc: deriveRoomDesc(r)
    };
}

function normalizeBooking(b) {
    return {
        id: b.id, roomId: b.roomId, roomName: b.roomName,
        userId: b.userId, userDisplayName: b.userDisplayName,
        date: b.date,
        start: b.startTime || b.start,
        end: b.endTime || b.end,
        status: b.status, comment: b.comment
    };
}

/* ---------- API-обёртки ---------- */
const getRooms = () => apiJson('/rooms').then(extractList);
const getFreeSlots = (id, date) => apiJson(`/rooms/${id}/free-slots?date=${date}`);
const getFreeRooms = (d, s, e) => apiJson(`/rooms/free?date=${d}&startTime=${s}&endTime=${e}`).then(extractList);
const getMyBookings = (st = '') => apiJson('/bookings/my' + (st ? `?status=${st}` : '')).then(extractList);
const getAdminBookings = (f = {}) => {
    const p = new URLSearchParams();
    ['date', 'roomId', 'userId', 'status'].forEach(k => f[k] && p.set(k, f[k]));
    return apiJson('/admin/bookings' + (p.toString() ? '?' + p : '')).then(extractList);
};
const getAdminStats = (from, to, building) => {
    const p = new URLSearchParams();
    if (from) p.set('from', from);
    if (to) p.set('to', to);
    if (building) p.set('building', building);
    return apiJson('/admin/stats' + (p.toString() ? '?' + p : ''));
};

/* ---------- загрузка ---------- */
async function loadRooms() {
    rooms = (await getRooms()).map(normalizeRoom);
}

async function loadMyBookings() {
    if (isGuest()) {
        myBookings = [];
        return;
    }
    myBookings = (await getMyBookings($('myStatusFilter').value || '')).map(normalizeBooking);
}

async function loadAllBookings() {
    if (!isAdmin()) {
        allBookings = [];
        return;
    }
    allBookings = (await getAdminBookings(getAdminFilterValues())).map(normalizeBooking);
}

async function reloadAfterMutation() {
    try {
        await loadMyBookings();
        if (isAdmin()) await loadAllBookings();
    } catch { /* ignore */
    }
    renderAll();
}

/* ---------- аутентификация ---------- */
function openLogin() {
    showLogin();
    $('authScreen').classList.remove('hidden');
    $('loginError').textContent = '';
    setTimeout(() => $('loginIdentity').focus(), 50);
}

function closeLogin() {
    $('authScreen').classList.add('hidden');
}

function showLogin() {
    $('registerForm').classList.add('hidden');
    $('loginForm').classList.remove('hidden');
    $('authTitle').textContent = 'Войти в CampusFlow';
    $('authSubtitle').textContent = 'Войдите, чтобы бронировать аудитории и управлять своими заявками.';
    $('registerError').textContent = '';
    setTimeout(() => $('loginIdentity').focus(), 40);
}

function showRegister() {
    $('loginForm').classList.add('hidden');
    $('registerForm').classList.remove('hidden');
    $('authTitle').textContent = 'Создать аккаунт';
    $('authSubtitle').textContent = 'Регистрация через API недоступна — используйте тестовые учётные записи.';
    $('loginError').textContent = '';
    $('registerError').textContent = '';
    setTimeout(() => $('registerName').focus(), 40);
}

$('loginForm').addEventListener('submit', async e => {
    e.preventDefault();
    const identity = $('loginIdentity').value.trim();
    const password = $('loginPassword').value;
    if (!identity || !password) return;
    try {
        const data = await apiJson('/auth/login', {
            method: 'POST',
            body: JSON.stringify({login: identity, password})
        });
        setToken(data.token);
        currentUserData = data.user;
        currentUser = data.user.login;
        closeLogin();
        await loadMyBookings();
        if (isAdmin()) await loadAllBookings();
        renderAll();
        $('loginIdentity').value = '';
        $('loginPassword').value = '';
        toast('Вы вошли в CampusFlow');
    } catch {
        $('loginError').textContent = 'Не удалось войти. Проверьте логин и пароль.';
    }
});
$('registerForm').addEventListener('submit', e => {
    e.preventDefault();
    $('registerError').textContent =
        'Регистрация через API недоступна. Используйте: student1/student1, student2/student2, admin/admin.';
});
$('showRegisterBtn').addEventListener('click', showRegister);
$('showLoginBtn').addEventListener('click', showLogin);
$('logoutBtn').addEventListener('click', logout);
$('loginOpenBtn').addEventListener('click', openLogin);
$('authScreen').addEventListener('click', e => {
    if (e.target === $('authScreen')) closeLogin();
});

function logout() {
    setToken('');
    currentUserData = null;
    currentUser = 'guest';
    myBookings = [];
    allBookings = [];
    setUser();
    renderAll();
    toast('Вы вышли из аккаунта');
}

/* ---------- навигация ---------- */
function go(id) {
    if ((id === 'bookings' || id === 'admin') && isGuest()) {
        openLogin();
        toast('Войдите, чтобы открыть личный кабинет');
        return;
    }
    if (id === 'admin' && currentUserData?.role !== 'admin') {
        toast('403 · Раздел доступен администратору');
        return;
    }
    document.querySelectorAll('.page').forEach(x => x.classList.remove('active'));
    $(id).classList.add('active');
    document.querySelectorAll('.nav button').forEach(x => x.classList.toggle('active', x.dataset.page === id));
    $('crumb').textContent = {
        overview: 'Обзор',
        rooms: 'Аудитории',
        bookings: 'Мои бронирования',
        admin: 'Администратор'
    }[id];
    if (id === 'rooms') renderRooms();
    if (id === 'bookings') renderMyBookings();
    if (id === 'admin') {
        renderAdmin();
        renderAdminStats();
    }
}

document.querySelectorAll('.nav button').forEach(b => b.addEventListener('click', () => go(b.dataset.page)));

/* ---------- пользователь ---------- */
function setUser() {
    const guest = isGuest();
    const u = currentUserData;
    $('loginOpenBtn').classList.toggle('hidden', !guest);
    $('avatar').classList.toggle('hidden', guest);
    $('userMeta').classList.toggle('hidden', guest);
    $('logoutBtn').classList.toggle('hidden', guest);
    if (!guest && u) {
        $('userName').textContent = u.displayName || u.login;
        $('userRole').textContent = (u.role === 'admin' ? 'Администратор' : 'Студент') + ' · ' + u.login;
        $('avatar').textContent = (u.displayName || u.login)
            .split(/\s+/).slice(0, 2).map(x => x[0]).join('').toUpperCase();
    }
    document.querySelector('[data-page="admin"]').classList.toggle('hidden', guest || u?.role !== 'admin');
}

/* ---------- карточки ---------- */
function roomCard(r) {
    const active = allBookings.filter(b => b.roomId === r.id && b.status === 'active').length;
    const pict = r.desc.includes('Компьютер') ? '💻'
        : r.capacity >= 60 ? '🎓'
            : r.desc.includes('Лаборатория') ? '🧪'
                : '✨';
    return `<article class="room-card">
    <div class="room-art"><span>${pict}</span><small>УУНиТ · CAMPUS SPACE</small></div>
    <div class="room-head">
      <div><div class="room-code">${escapeHtml(r.name)}</div>
      <div class="room-sub">Корпус ${escapeHtml(r.building)} · ${r.floor} этаж</div></div>
      <span class="availability">${active ? 'Есть свободные слоты' : 'Доступна'}</span>
    </div>
    <div class="room-facts"><span>♙ ${r.capacity} мест</span><span>▧ ${escapeHtml(r.desc)}</span></div>
    <div class="tags">${r.equipment.map(x => `<span class="tag">${escapeHtml(x)}</span>`).join('')}</div>
    <div class="room-footer"><small>Оборудование и параметры</small>
      <button onclick="openModal(${r.id})">Забронировать →</button></div>
  </article>`;
}

function renderRooms() {
    const q = $('searchRoom').value.toLowerCase();
    const b = $('filterBuilding').value;
    const c = +$('filterCapacity').value;
    const e = $('filterEquipment').value;
    const list = rooms.filter(r =>
        (!q || r.name.toLowerCase().includes(q)) &&
        (!b || r.building === b) && r.capacity >= c &&
        (!e || r.equipment.includes(e)));
    $('allRooms').innerHTML = list.length
        ? list.map(roomCard).join('')
        : '<div class="empty">По заданным параметрам аудитории не найдены.</div>';
}

function renderFeaturedList(list) {
    $('featuredRooms').innerHTML = list.length
        ? list.slice(0, 3).map(roomCard).join('')
        : '<div class="empty">Ничего не найдено. Измените фильтры.</div>';
}

['searchRoom', 'filterBuilding', 'filterCapacity', 'filterEquipment'].forEach(id =>
    $(id).addEventListener('input', renderRooms));

/* ---------- быстрый поиск (главная) ---------- */
async function applyQuick() {
    const b = $('quickBuilding').value;
    const c = +$('quickCapacity').value;
    const e = $('quickEquipment').value;
    const date = $('quickDate').value;
    const start = $('quickStart').value;
    const end = $('quickEnd').value;
    let pool = rooms;
    let usedFreeSearch = false;
    if (date && start && end) {
        if (toMin(start) >= toMin(end)) {
            toast('Время окончания должно быть позже начала');
            return;
        }
        try {
            const freeRooms = await getFreeRooms(date, start, end);
            const freeIds = new Set(freeRooms.map(r => r.id));
            pool = rooms.filter(r => freeIds.has(r.id));
            usedFreeSearch = true;
        } catch (err) {
            toast('Не удалось проверить свободные аудитории: ' + err.message);
        }
    }
    const list = pool.filter(r =>
        (!b || r.building === b) && r.capacity >= c && (!e || r.equipment.includes(e)));
    renderFeaturedList(list);
    toast(usedFreeSearch ? `Найдено ${list.length} свободных аудиторий` : 'Результаты поиска обновлены');
}

/* ---------- модалка и свободные слоты ---------- */
function rebuildEndTimeOptions() {
    const sel = $('endTime');
    const startM = toMin($('startTime').value);
    const opts = [];
    for (let m = startM + 30; m <= 18 * 60; m += 30) opts.push(fromMin(m));
    const current = sel.value;
    sel.innerHTML = opts.map(t => `<option>${t}</option>`).join('');
    sel.value = opts.includes(current) ? current : (opts[0] || '');
}

async function loadModalFreeSlots() {
    if (!selectedRoom) return;
    const date = $('bookingDate').value;
    if (!date) return;
    const container = $('modalFreeSlots');
    container.innerHTML = '<div class="muted" style="font-size:11px;padding:6px 0">Загрузка свободных слотов…</div>';
    try {
        const data = await getFreeSlots(selectedRoom.id, date);
        freeSlots = data.freeSlots || [];
        renderModalFreeSlots();
    } catch {
        container.innerHTML = '<div class="empty" style="padding:8px">Не удалось загрузить слоты</div>';
    }
}

function renderModalFreeSlots() {
    const container = $('modalFreeSlots');
    if (!freeSlots.length) {
        container.innerHTML = '<div class="empty" style="padding:8px">На эту дату свободных окон нет</div>';
        return;
    }
    container.innerHTML = `<div class="free-slots-label">Свободные интервалы</div>
    <div class="free-slots-list">${freeSlots.map(s =>
        `<button type="button" class="free-slot" data-start="${s.startTime}" data-end="${s.endTime}">
        ${s.startTime}–${s.endTime}</button>`).join('')}</div>`;
    container.querySelectorAll('.free-slot').forEach(btn => {
        btn.addEventListener('click', () => {
            $('startTime').value = btn.dataset.start;
            rebuildEndTimeOptions();
            $('endTime').value = btn.dataset.end;
            container.querySelectorAll('.free-slot').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
        });
    });
}

function openModal(id) {
    if (isGuest()) {
        openLogin();
        toast('Войдите, чтобы забронировать аудиторию');
        return;
    }
    selectedRoom = rooms.find(r => r.id === id);
    $('modalRoom').textContent = `${selectedRoom.name} · корпус ${selectedRoom.building} · ${selectedRoom.capacity} мест`;
    $('bookingDate').value = $('quickDate').value || dateISO(tomorrow);
    $('bookingDate').min = dateISO(today);
    $('modal').classList.remove('hidden');
    rebuildEndTimeOptions();
    loadModalFreeSlots();
}

function closeModal() {
    $('modal').classList.add('hidden');
}

async function confirmBooking() {
    const date = $('bookingDate').value;
    const start = $('startTime').value;
    const end = $('endTime').value;
    const comment = $('bookingComment').value;
    if (!selectedRoom || !date) {
        toast('Выберите аудиторию и дату');
        return;
    }
    if (toMin(start) >= toMin(end)) {
        toast('Время окончания должно быть позже начала');
        return;
    }
    if (toMin(start) < 540 || toMin(end) > 1080) {
        toast('Рабочий день: 09:00–18:00');
        return;
    }
    if (toMin(start) % 30 || toMin(end) % 30) {
        toast('Шаг времени — 30 минут');
        return;
    }
    try {
        await apiJson('/bookings', {
            method: 'POST',
            body: JSON.stringify({roomId: selectedRoom.id, date, startTime: start, endTime: end, comment})
        });
        closeModal();
        $('bookingComment').value = '';
        await reloadAfterMutation();
        toast('Бронирование успешно создано');
    } catch (err) {
        toast(apiErrorMessage(err));
    }
}

$('bookingDate').addEventListener('change', loadModalFreeSlots);
$('startTime').addEventListener('change', rebuildEndTimeOptions);

/* ---------- мои бронирования ---------- */
function renderMyBookings() {
    const list = myBookings.slice().sort((a, b) => a.date.localeCompare(b.date));
    $('myBookings').innerHTML = list.length
        ? list.map(b => {
            const r = rooms.find(x => x.id === b.roomId) || {name: b.roomName || `#${b.roomId}`, desc: ''};
            return `<tr>
          <td><b>${escapeHtml(r.name)}</b><div class="muted">${escapeHtml(r.desc)}</div></td>
          <td>${b.date}</td>
          <td>${b.start}–${b.end}</td>
          <td>${escapeHtml(b.comment || '—')}</td>
          <td><span class="status ${b.status === 'cancelled' ? 'cancelled' : ''}">${b.status === 'active' ? 'Активна' : 'Отменена'}</span></td>
          <td>${b.status === 'active' ? `<button class="btn btn-danger" onclick="cancelBooking(${b.id})">Отменить</button>` : '—'}</td>
        </tr>`;
        }).join('')
        : '<tr><td colspan="6" class="empty">У вас пока нет бронирований.</td></tr>';
}

async function cancelBooking(id) {
    try {
        await apiJson('/bookings/' + id, {method: 'DELETE'});
        await reloadAfterMutation();
        toast('Бронь отменена, слот снова свободен');
    } catch (err) {
        toast(apiErrorMessage(err));
    }
}

$('myStatusFilter').addEventListener('change', async () => {
    try {
        await loadMyBookings();
        renderMyBookings();
        updateStats();
        renderUpcoming();
    } catch (err) {
        toast(apiErrorMessage(err));
    }
});

/* ---------- админ: фильтры и экспорт ---------- */
function getAdminFilterValues() {
    return {
        date: $('adminFilterDate').value,
        roomId: $('adminFilterRoom').value,
        userId: $('adminFilterUser').value,
        status: $('adminFilterStatus').value
    };
}

function refreshAdminRoomOptions() {
    const sel = $('adminFilterRoom');
    const current = sel.value;
    sel.innerHTML = '<option value="">Все</option>' +
        rooms.slice().sort((a, b) => a.name.localeCompare(b.name, 'ru'))
            .map(r => `<option value="${r.id}">${escapeHtml(r.name)}</option>`).join('');
    if ([...sel.options].some(o => o.value === current)) sel.value = current;
}

function refreshAdminUserFilter() {
    const sel = $('adminFilterUser');
    const users = new Map();
    allBookings.forEach(b => {
        if (b.userId && b.userDisplayName) users.set(b.userId, b.userDisplayName);
    });
    const current = sel.value;
    sel.innerHTML = '<option value="">Все пользователи</option>' +
        [...users.entries()].map(([id, name]) =>
            `<option value="${id}">${escapeHtml(name)}</option>`).join('');
    if ([...sel.options].some(o => o.value === current)) sel.value = current;
}

async function applyAdminFilters() {
    try {
        allBookings = (await getAdminBookings(getAdminFilterValues())).map(normalizeBooking);
        renderAdmin();
    } catch (err) {
        toast(apiErrorMessage(err));
    }
}

async function resetAdminFilters() {
    ['adminFilterDate', 'adminFilterRoom', 'adminFilterUser', 'adminFilterStatus'].forEach(id => {
        $(id).value = '';
    });
    await applyAdminFilters();
}

async function exportBookingsCsv() {
    if (!isAdmin()) {
        toast('403 · Только администратор');
        return;
    }
    const f = getAdminFilterValues();
    const p = new URLSearchParams();
    ['date', 'roomId', 'userId', 'status'].forEach(k => f[k] && p.set(k, f[k]));
    try {
        const res = await fetch(API_BASE + '/admin/bookings/export' + (p.toString() ? '?' + p : ''), {
            headers: {Authorization: 'Bearer ' + token}
        });
        if (res.status === 401) {
            handleUnauthorized();
            return;
        }
        if (!res.ok) throw new Error('HTTP ' + res.status);
        const blob = await res.blob();
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `campusflow-bookings-${dateISO(new Date())}.csv`;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 200);
        toast('CSV выгружен');
    } catch (err) {
        toast('Не удалось выгрузить CSV: ' + err.message);
    }
}

$('adminApplyFilters').addEventListener('click', applyAdminFilters);
$('adminResetFilters').addEventListener('click', resetAdminFilters);
$('adminExportCsv').addEventListener('click', exportBookingsCsv);

/* ---------- админ: аудитории ---------- */
function renderAdminRooms() {
    if ($('adminRoomCount')) $('adminRoomCount').textContent = rooms.length + ' помещений';
    $('adminRooms').innerHTML = rooms.length
        ? rooms.slice().sort((a, b) => a.name.localeCompare(b.name, 'ru')).map(r =>
            `<tr>
          <td><b>${escapeHtml(r.name)}</b></td>
          <td>${escapeHtml(r.building)} · ${r.floor} этаж</td>
          <td>${r.capacity}</td>
          <td>${escapeHtml(r.desc)}</td>
          <td>${(r.equipment || []).map(x => `<span class="tag">${escapeHtml(x)}</span>`).join(' ') || '—'}</td>
          <td class="room-actions">
            <button class="btn btn-soft" onclick="editRoom(${r.id})">Изменить</button>
            <button class="btn btn-danger" onclick="deleteRoom(${r.id})">Удалить</button>
          </td></tr>`).join('')
        : '<tr><td colspan="6" class="empty">Аудиторий пока нет.</td></tr>';
}

function renderAdmin() {
    if (!isAdmin()) return;
    $('adminRoomStat').textContent = rooms.length;
    $('adminTotal').textContent = allBookings.length;
    $('adminActive').textContent = allBookings.filter(b => b.status === 'active').length;
    const buildings = [...new Set(rooms.map(r => r.building))].sort();
    $('adminBuildingCount').textContent = buildings.length;
    renderAdminRooms();
    refreshAdminUserFilter();

    $('allBookings').innerHTML = allBookings.length
        ? allBookings.map(b => {
            const r = rooms.find(x => x.id === b.roomId);
            const roomName = r?.name || b.roomName || `#${b.roomId}`;
            const userName = b.userDisplayName || `ID ${b.userId}`;
            return `<tr>
          <td>${escapeHtml(userName)}</td>
          <td><b>${escapeHtml(roomName)}</b></td>
          <td>${b.date}</td>
          <td>${b.start}–${b.end}</td>
          <td><span class="status ${b.status === 'cancelled' ? 'cancelled' : ''}">${b.status === 'active' ? 'Активна' : 'Отменена'}</span></td>
          <td>${b.status === 'active' ? `<button class="btn btn-danger" onclick="adminCancel(${b.id})">Отменить</button>` : '—'}</td>
        </tr>`;
        }).join('')
        : '<tr><td colspan="6" class="empty">Бронирований по заданным фильтрам нет.</td></tr>';
}

async function adminCancel(id) {
    if (!isAdmin()) {
        toast('403 · Только администратор');
        return;
    }
    try {
        await apiJson('/admin/bookings/' + id, {method: 'DELETE'});
        await reloadAfterMutation();
        toast('Бронирование отменено администратором');
    } catch (err) {
        toast(apiErrorMessage(err));
    }
}

function resetRoomForm() {
    editingRoomId = null;
    $('roomForm').reset();
    $('roomFloor').value = 1;
    $('roomFormTitle').textContent = 'Добавить аудиторию';
    $('saveRoomBtn').textContent = 'Добавить аудиторию';
    $('cancelRoomEdit').classList.add('hidden');
}

function editRoom(id) {
    if (!isAdmin()) {
        toast('403 · Только администратор');
        return;
    }
    const r = rooms.find(x => x.id === id);
    if (!r) return;
    editingRoomId = id;
    $('roomName').value = r.name;
    $('roomBuilding').value = r.building;
    $('roomFloor').value = r.floor;
    $('roomCapacity').value = r.capacity;
    $('roomDesc').value = r.desc;
    $('roomEquipment').value = (r.equipment || []).join(', ');
    $('roomFormTitle').textContent = 'Редактировать аудиторию ' + r.name;
    $('saveRoomBtn').textContent = 'Сохранить изменения';
    $('cancelRoomEdit').classList.remove('hidden');
    $('roomForm').scrollIntoView({behavior: 'smooth', block: 'center'});
}

async function deleteRoom(id) {
    if (!isAdmin()) {
        toast('403 · Только администратор');
        return;
    }
    const r = rooms.find(x => x.id === id);
    if (!r) return;
    if (!confirm(`Удалить аудиторию ${r.name}?`)) return;
    try {
        await apiJson('/admin/rooms/' + id, {method: 'DELETE'});
        await loadRooms();
        renderAll();
        toast('Аудитория удалена');
    } catch (err) {
        toast(apiErrorMessage(err));
    }
}

$('roomForm').addEventListener('submit', async e => {
    e.preventDefault();
    if (!isAdmin()) {
        toast('403 · Только администратор');
        return;
    }
    const name = $('roomName').value.trim().toUpperCase();
    const building = $('roomBuilding').value;
    const floor = Number($('roomFloor').value);
    const capacity = Number($('roomCapacity').value);
    const equipmentRu = $('roomEquipment').value.split(',').map(x => x.trim()).filter(Boolean);
    const equipment = equipmentRu.map(x => EQUIPMENT_FROM_RU[x.toLowerCase()] || x);
    if (!name || !['A', 'B', 'C', 'D'].includes(building) ||
        !Number.isInteger(floor) || floor < 1 ||
        !Number.isInteger(capacity) || capacity < 1) {
        toast('400 · Проверьте поля аудитории');
        return;
    }
    const payload = {name, building, floor, capacity, equipment};
    try {
        if (editingRoomId !== null) {
            await apiJson('/admin/rooms/' + editingRoomId, {method: 'PUT', body: JSON.stringify(payload)});
            toast('Изменения аудитории сохранены');
        } else {
            await apiJson('/admin/rooms', {method: 'POST', body: JSON.stringify(payload)});
            toast('Аудитория добавлена');
        }
        editingRoomId = null;
        await loadRooms();
        resetRoomForm();
        renderAll();
    } catch (err) {
        toast(apiErrorMessage(err));
    }
});

/* ---------- админ: статистика ---------- */
async function loadAdminStats() {
    if (!isAdmin()) {
        $('statsResult').innerHTML = '<div class="empty">Статистика доступна только администратору.</div>';
        return;
    }

    const from = $('statsFrom').value;
    const to = $('statsTo').value;
    const building = $('statsBuilding').value;

    if (from && to && from > to) {
        $('statsResult').innerHTML = '<div class="stats-error">Начальная дата не может быть позже конечной.</div>';
        return;
    }

    const button = $('statsLoadBtn');
    button.disabled = true;
    button.textContent = 'Загрузка…';
    $('statsResult').innerHTML = '<div class="empty">Загружаем статистику…</div>';

    try {
        adminStats = await getAdminStats(from, to, building);
        renderAdminStats();
    } catch (err) {
        console.error('Ошибка загрузки статистики:', err);
        $('statsResult').innerHTML = `<div class="stats-error">Не удалось загрузить статистику: ${escapeHtml(apiErrorMessage(err))}</div>`;
    } finally {
        button.disabled = false;
        button.textContent = 'Показать';
    }
}

function getStatValue(object, ...keys) {
    if (!object || typeof object !== 'object') return undefined;
    for (const key of keys) {
        if (object[key] !== undefined && object[key] !== null) return object[key];
        const actualKey = Object.keys(object).find(k => k.toLowerCase() === key.toLowerCase());
        if (actualKey && object[actualKey] !== undefined && object[actualKey] !== null) {
            return object[actualKey];
        }
    }
    return undefined;
}

function formatHours(minutes) {
    return `${(Number(minutes || 0) / 60).toFixed(1)} ч`;
}

function renderLoadBucket(item = {}) {
    // API может вернуть Load как вложенный объект или показатели непосредственно в строке.
    const load = getStatValue(item, 'load', 'Load') || item;
    return {
        active: Number(getStatValue(load, 'activeBookings', 'ActiveBookings') || 0),
        cancelled: Number(getStatValue(load, 'cancelledBookings', 'CancelledBookings') || 0),
        booked: Number(getStatValue(load, 'bookedMinutes', 'BookedMinutes') || 0),
        total: Number(getStatValue(load, 'totalMinutes', 'TotalMinutes') || 0),
        utilization: Number(getStatValue(load, 'utilizationPercent', 'UtilizationPercent') || 0)
    };
}

function renderStatsTable(headers, rows, emptyText = 'Данных за выбранный период нет') {
    if (!rows.length) return `<div class="empty">${emptyText}</div>`;

    return `
  <div class="table-wrap">
   <table class="stats-table">
    <thead><tr>${headers.map(header => `<th>${escapeHtml(header)}</th>`).join('')}</tr></thead>
    <tbody>${rows.join('')}</tbody>
   </table>
  </div>`;
}

function renderAdminStats() {
    const container = $('statsResult');
    if (!container) return;

    if (!adminStats) {
        container.innerHTML = '<div class="empty">Выберите период и нажмите «Показать»</div>';
        return;
    }

    // Поддерживаем как прямой ответ API, так и обёртку { data: {...} }.
    const data = getStatValue(adminStats, 'data') || adminStats;
    const totals = renderLoadBucket(getStatValue(data, 'totals', 'Totals') || {});
    const byDay = getStatValue(data, 'byDay', 'ByDay') || [];
    const byBuilding = getStatValue(data, 'byBuilding', 'ByBuilding') || [];
    const topRooms = getStatValue(data, 'topRooms', 'TopRooms') || [];

    const totalsHtml = `
  <div class="stats">
   <div class="stat"><div class="stat-label">Активные бронирования</div><div class="stat-value">${totals.active}</div><div class="stat-note">Не отменены</div></div>
   <div class="stat"><div class="stat-label">Отменённые бронирования</div><div class="stat-value">${totals.cancelled}</div><div class="stat-note">За выбранный период</div></div>
   <div class="stat"><div class="stat-label">Занятое время</div><div class="stat-value">${formatHours(totals.booked)}</div><div class="stat-note">Суммарно</div></div>
   <div class="stat"><div class="stat-label">Загрузка аудиторий</div><div class="stat-value">${totals.utilization.toFixed(1)}%</div><div class="stat-note">От доступного времени</div></div>
  </div>`;

    const dailyRows = byDay.map(item => {
        const load = renderLoadBucket(item);
        const date = getStatValue(item, 'date', 'Date') || '—';
        return `<tr>
   <td>${escapeHtml(date)}</td><td>${load.active}</td><td>${load.cancelled}</td>
   <td>${formatHours(load.booked)}</td><td>${load.utilization.toFixed(1)}%</td>
  </tr>`;
    });

    const buildingRows = byBuilding.map(item => {
        const load = renderLoadBucket(item);
        return `<tr>
   <td>${escapeHtml(getStatValue(item, 'building', 'Building') || '—')}</td>
   <td>${Number(getStatValue(item, 'rooms', 'Rooms') || 0)}</td>
   <td>${load.active}</td><td>${formatHours(load.booked)}</td>
   <td>${load.utilization.toFixed(1)}%</td>
  </tr>`;
    });

    const roomRows = topRooms.map(item => `
  <tr>
   <td>${escapeHtml(getStatValue(item, 'roomName', 'RoomName') || '—')}</td>
   <td>${escapeHtml(getStatValue(item, 'building', 'Building') || '—')}</td>
   <td>${Number(getStatValue(item, 'activeBookings', 'ActiveBookings') || 0)}</td>
   <td>${formatHours(getStatValue(item, 'bookedMinutes', 'BookedMinutes') || 0)}</td>
  </tr>`);

    container.innerHTML = `
  <section class="stats-section">
   <h3>Общая статистика</h3>
   ${totalsHtml}
  </section>
  <section class="stats-section">
   <h3>Загрузка по дням</h3>
   ${renderStatsTable(['Дата', 'Активные брони', 'Отменённые', 'Занятое время', 'Загрузка'], dailyRows)}
  </section>
  <section class="stats-section">
   <h3>Загрузка по корпусам</h3>
   ${renderStatsTable(['Корпус', 'Аудиторий', 'Активные брони', 'Занятое время', 'Загрузка'], buildingRows)}
  </section>
  <section class="stats-section">
   <h3>Самые загруженные аудитории</h3>
   ${renderStatsTable(['Аудитория', 'Корпус', 'Активные брони', 'Занятое время'], roomRows)}
  </section>`;
}

$('statsLoadBtn').addEventListener('click', loadAdminStats);


/* ---------- вспомогательные ---------- */
function updateBuildingStats() {
    const buildings = [...new Set(rooms.map(r => r.building))].sort();
    if ($('buildingCount')) $('buildingCount').textContent = buildings.length;
    if ($('buildingList')) $('buildingList').textContent = buildings.length ? buildings.join(', ') : 'Нет корпусов';
    if ($('adminBuildingCount')) $('adminBuildingCount').textContent = buildings.length;
    const heroNote = document.querySelector('.hero-note');
    if (heroNote) heroNote.textContent = rooms.length + ' пространств · ' + buildings.length + ' корпуса';
    const datePill = document.querySelector('#rooms .date-pill');
    if (datePill) datePill.textContent = rooms.length + ' аудиторий · ' + buildings.length + ' корпуса';
}

function updateStats() {
    const todayStr = dateISO(new Date());
    if ($('myCount')) $('myCount').textContent = myBookings.filter(b => b.status === 'active').length;
    if ($('totalRooms')) $('totalRooms').textContent = rooms.length;
    if ($('freeRooms')) {
        const occupied = new Set(
            allBookings.filter(b => b.date === todayStr && b.status === 'active').map(b => b.roomId));
        $('freeRooms').textContent = isAdmin() ? rooms.filter(r => !occupied.has(r.id)).length : rooms.length;
    }
}

function renderUpcoming() {
    const todayStr = dateISO(new Date());
    const list = myBookings
        .filter(b => b.status === 'active' && b.date >= todayStr)
        .sort((a, b) => (a.date + a.start).localeCompare(b.date + b.start))
        .slice(0, 3);
    $('upcoming').innerHTML = list.length
        ? list.map(b => {
            const r = rooms.find(x => x.id === b.roomId) || {name: b.roomName || '—', building: '?'};
            return `<div class="quick-item">
          <div class="quick-room">
            <div class="room-mini">${escapeHtml(r.name)}</div>
            <div><strong>${b.date}</strong><small>${b.start}–${b.end} · корпус ${escapeHtml(r.building || '?')}</small></div>
          </div>
          <span class="status">Активна</span>
        </div>`;
        }).join('')
        : '<div class="empty">Ближайших броней нет.</div>';
}

function renderAll() {
    updateBuildingStats();
    updateStats();
    renderRooms();
    renderMyBookings();
    renderAdmin();
    renderUpcoming();
    refreshAdminRoomOptions();
    setUser();
}

/* ---------- старт ---------- */
async function init() {
    $('todayLabel').textContent = new Intl.DateTimeFormat('ru-RU',
        {day: 'numeric', month: 'long', year: 'numeric'}).format(today);
    $('quickDate').value = dateISO(tomorrow);
    $('quickDate').min = dateISO(today);

    // Сначала проверяем сохранённый токен. Ошибка сети не удаляет токен:
    // только подтверждённый ответ 401 означает, что сессия недействительна.
    if (token) {
        try {
            currentUserData = await apiJson('/auth/me');
            currentUser = currentUserData?.login || 'guest';
            if (currentUser === 'guest') {
                setToken('');
                currentUserData = null;
            }
        } catch (err) {
            if (err.status !== 401) {
                console.warn('Не удалось проверить сохранённую сессию:', err);
                toast('Не удалось проверить сессию: сервер недоступен');
            }
        }
    }

    try {
        await loadRooms();
    } catch (err) {
        console.error('Не удалось загрузить аудитории:', err);
        toast('Не удалось загрузить каталог аудиторий');
    }

    if (!isGuest()) {
        try {
            await loadMyBookings();
        } catch (err) {
            if (err.status !== 401) console.error(err);
        }

        if (isAdmin()) {
            try {
                await loadAllBookings();
            } catch (err) {
                if (err.status !== 401) console.error(err);
            }
        }
    }

    renderAll();
    renderFeaturedList(rooms);
}

init();

/* ---------- экспорт для inline-обработчиков HTML ---------- */
window.go = go;
window.openLogin = openLogin;
window.closeLogin = closeLogin;
window.openModal = openModal;
window.closeModal = closeModal;
window.confirmBooking = confirmBooking;
window.cancelBooking = cancelBooking;
window.adminCancel = adminCancel;
window.editRoom = editRoom;
window.deleteRoom = deleteRoom;
window.resetRoomForm = resetRoomForm;
window.applyQuick = applyQuick;
window.renderRooms = renderRooms;