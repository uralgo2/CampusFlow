const API_BASE   = '/api';
const TOKEN_KEY  = 'campusflowToken';

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
const today = new Date();
const tomorrow = new Date(); tomorrow.setDate(today.getDate() + 1);

let token = localStorage.getItem(TOKEN_KEY) || '';
let currentUserData = null;   // { id, login, displayName, role }
let currentUser = 'guest';    // логин или 'guest'
let rooms = [];               // нормализованные аудитории
let myBookings = [];          // мои брони
let allBookings = [];         // все брони (только для админа)
let selectedRoom = null;
let editingRoomId = null;

/* ---------- базовые утилиты ---------- */
function escapeHtml(v) {
 return String(v ?? '').replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));
}
function toast(s) {
 const n = document.createElement('div');
 n.className = 'toast'; n.textContent = s;
 document.body.append(n);
 setTimeout(() => n.remove(), 2700);
}
function isGuest()  { return currentUser === 'guest'; }
function isAdmin()  { return !isGuest() && currentUserData?.role === 'admin'; }

function setToken(t) {
 token = t || '';
 if (t) localStorage.setItem(TOKEN_KEY, t);
 else   localStorage.removeItem(TOKEN_KEY);
}

async function apiRequest(path, options = {}) {
 const headers = { 'Content-Type': 'application/json', ...(options.headers || {}) };
 if (token) headers.Authorization = 'Bearer ' + token;

 const res = await fetch(API_BASE + path, { ...options, headers });

 if (res.status === 401) {
  setToken(''); currentUserData = null; currentUser = 'guest';
  handleUnauthorized();
 }
 return res;
}

async function apiJson(path, options = {}) {
 const res = await apiRequest(path, options);
 if (!res.ok) {
  let data = null;
  try { data = await res.json(); } catch { /* ignore */ }
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
  VALIDATION_ERROR:   'Проверьте введённые данные',
  UNAUTHORIZED:       'Требуется авторизация',
  FORBIDDEN:          'Недостаточно прав',
  ROOM_NOT_FOUND:     'Аудитория не найдена',
  BOOKING_NOT_FOUND:  'Бронирование не найдено',
  BOOKING_CONFLICT:   'Аудитория уже забронирована на это время',
  ALREADY_CANCELLED:  'Бронирование уже отменено',
  ROOM_IN_USE:        'Нельзя удалить аудиторию: с ней связаны бронирования'
 };
 if (err?.code && map[err.code]) return map[err.code];
 return err?.message || 'Произошла ошибка';
}

function handleUnauthorized() {
 $('loginOpenBtn').classList.remove('hidden');
 $('avatar').classList.add('hidden');
 $('userMeta').classList.add('hidden');
 $('logoutBtn').classList.add('hidden');
 document.querySelector('[data-page="admin"]').classList.add('hidden');
 toast('Сессия истекла. Войдите снова.');
}

function extractList(data) {
 if (Array.isArray(data)) return data;
 if (data && Array.isArray(data.items)) return data.items;
 if (data && Array.isArray(data.data))  return data.data;
 return [];
}

function deriveRoomDesc(r) {
 if (r.description) return r.description;
 if (r.desc)        return r.desc;
 const eq = (r.equipment || []).map(x => String(x).toLowerCase());
 if (eq.includes('computers') || eq.includes('компьютеры')) return 'Компьютерный класс';
 if ((r.capacity || 0) >= 50) return 'Лекционная аудитория';
 if ((r.capacity || 0) <= 15) return 'Малая аудитория';
 return 'Учебная аудитория';
}

function normalizeRoom(r) {
 return {
  id: r.id,
  name: r.name,
  building: r.building,
  floor: r.floor,
  capacity: r.capacity,
  equipment: (r.equipment || []).map(c => EQUIPMENT_TO_RU[c] || c),
  desc: deriveRoomDesc(r)
 };
}

function normalizeBooking(b) {
 return {
  id: b.id,
  roomId: b.roomId,
  roomName: b.roomName,
  userId: b.userId,
  userDisplayName: b.userDisplayName,
  date: b.date,
  start: b.startTime || b.start,
  end:   b.endTime   || b.end,
  status: b.status,
  comment: b.comment
 };
}

/* ---------- загрузка данных ---------- */
async function loadRooms() {
 rooms = extractList(await apiJson('/rooms')).map(normalizeRoom);
}
async function loadMyBookings() {
 if (isGuest()) { myBookings = []; return; }
 myBookings = extractList(await apiJson('/bookings/my')).map(normalizeBooking);
}
async function loadAllBookings() {
 if (!isAdmin()) { allBookings = []; return; }
 allBookings = extractList(await apiJson('/admin/bookings')).map(normalizeBooking);
}

async function refreshAll() {
 try {
  await loadRooms();
  await loadMyBookings();
  await loadAllBookings();
  renderAll();
 } catch (err) {
  toast('Не удалось загрузить данные: ' + err.message);
 }
}

/* ---------- аутентификация ---------- */
function openLogin() {
 showLogin();
 $('authScreen').classList.remove('hidden');
 $('loginError').textContent = '';
 setTimeout(() => $('loginIdentity').focus(), 50);
}
function closeLogin() { $('authScreen').classList.add('hidden'); }

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
 $('authSubtitle').textContent = 'Регистрация через API пока недоступна — используйте тестовые учётные записи.';
 $('loginError').textContent = ''; $('registerError').textContent = '';
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
   body: JSON.stringify({ login: identity, password })
  });
  setToken(data.token);
  currentUserData = data.user;
  currentUser = data.user.login;
  closeLogin();
  await refreshAll();
  setUser();
  $('loginIdentity').value = '';
  $('loginPassword').value = '';
  toast('Вы вошли в CampusFlow');
 } catch (err) {
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
$('authScreen').addEventListener('click', e => { if (e.target === $('authScreen')) closeLogin(); });

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
  openLogin(); toast('Войдите, чтобы открыть личный кабинет'); return;
 }
 if (id === 'admin' && currentUserData?.role !== 'admin') {
  toast('403 · Раздел доступен администратору'); return;
 }
 document.querySelectorAll('.page').forEach(x => x.classList.remove('active'));
 $(id).classList.add('active');
 document.querySelectorAll('.nav button').forEach(x => x.classList.toggle('active', x.dataset.page === id));
 $('crumb').textContent = { overview: 'Обзор', rooms: 'Аудитории', bookings: 'Мои бронирования', admin: 'Администратор' }[id];
 if (id === 'rooms')    renderRooms();
 if (id === 'bookings') renderMyBookings();
 if (id === 'admin')    renderAdmin();
}
document.querySelectorAll('.nav button').forEach(b => b.addEventListener('click', () => go(b.dataset.page)));

/* ---------- отображение пользователя ---------- */
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

/* ---------- карточки аудиторий ---------- */
function roomCard(r) {
 const active = allBookings.filter(b => b.roomId === r.id && b.status === 'active').length;
 const pict = r.desc.includes('Компьютер') ? '💻'
     : r.capacity >= 60             ? '🎓'
         : r.desc.includes('Лаборатория')? '🧪'
             : '✨';
 return `<article class="room-card">
    <div class="room-art"><span>${pict}</span><small>УУНиТ · CAMPUS SPACE</small></div>
    <div class="room-head">
      <div>
        <div class="room-code">${escapeHtml(r.name)}</div>
        <div class="room-sub">Корпус ${escapeHtml(r.building)} · ${r.floor} этаж</div>
      </div>
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
     (!b || r.building === b) &&
     r.capacity >= c &&
     (!e || r.equipment.includes(e))
 );
 $('allRooms').innerHTML = list.length
     ? list.map(roomCard).join('')
     : '<div class="empty">По заданным параметрам аудитории не найдены.</div>';
}

function renderFeatured() {
 const b = $('quickBuilding')?.value || '';
 const c = +($('quickCapacity')?.value || 0);
 const e = $('quickEquipment')?.value || '';
 const list = rooms.filter(r =>
     (!b || r.building === b) &&
     r.capacity >= c &&
     (!e || r.equipment.includes(e))
 );
 $('featuredRooms').innerHTML = list.length
     ? list.slice(0, 3).map(roomCard).join('')
     : '<div class="empty">Ничего не найдено. Измените фильтры.</div>';
}

function applyQuick() { renderFeatured(); toast('Результаты поиска обновлены'); }

['searchRoom', 'filterBuilding', 'filterCapacity', 'filterEquipment'].forEach(id =>
    $(id).addEventListener('input', renderRooms));

/* ---------- модальное окно бронирования ---------- */
function openModal(id) {
 if (isGuest()) { openLogin(); toast('Войдите, чтобы забронировать аудиторию'); return; }
 selectedRoom = rooms.find(r => r.id === id);
 $('modalRoom').textContent = `${selectedRoom.name} · корпус ${selectedRoom.building} · ${selectedRoom.capacity} мест`;
 $('bookingDate').value = $('quickDate').value || dateISO(tomorrow);
 $('bookingDate').min = dateISO(today);
 $('modal').classList.remove('hidden');
}
function closeModal() { $('modal').classList.add('hidden'); }

async function confirmBooking() {
 const date = $('bookingDate').value;
 const start = $('startTime').value;
 const end = $('endTime').value;
 const comment = $('bookingComment').value;
 const mins = t => { const [h, m] = t.split(':').map(Number); return h * 60 + m; };

 if (!selectedRoom || !date) { toast('Выберите аудиторию и дату'); return; }
 if (mins(start) >= mins(end)) { toast('Время окончания должно быть позже начала'); return; }
 if (mins(start) < 540 || mins(end) > 1080) { toast('Рабочий день: 09:00–18:00'); return; }
 if (mins(start) % 30 || mins(end) % 30) { toast('Шаг времени — 30 минут'); return; }

 try {
  await apiJson('/bookings', {
   method: 'POST',
   body: JSON.stringify({ roomId: selectedRoom.id, date, startTime: start, endTime: end, comment })
  });
  closeModal();
  $('bookingComment').value = '';
  await loadMyBookings();
  await loadAllBookings();
  renderAll();
  toast('Бронирование успешно создано');
 } catch (err) {
  toast(apiErrorMessage(err));
 }
}

/* ---------- мои бронирования ---------- */
function renderMyBookings() {
 const list = myBookings.slice().sort((a, b) => a.date.localeCompare(b.date));
 $('myBookings').innerHTML = list.length
     ? list.map(b => {
      const r = rooms.find(x => x.id === b.roomId) || { name: b.roomName || `#${b.roomId}`, desc: '' };
      return `<tr>
          <td><b>${escapeHtml(r.name)}</b><div class="muted">${escapeHtml(r.desc)}</div></td>
          <td>${b.date}</td>
          <td>${b.start}–${b.end}</td>
          <td>${b.comment || '—'}</td>
          <td><span class="status ${b.status === 'cancelled' ? 'cancelled' : ''}">${b.status === 'active' ? 'Активна' : 'Отменена'}</span></td>
          <td>${b.status === 'active' ? `<button class="btn btn-danger" onclick="cancelBooking(${b.id})">Отменить</button>` : '—'}</td>
        </tr>`;
     }).join('')
     : '<tr><td colspan="6" class="empty">У вас пока нет бронирований.</td></tr>';
}

async function cancelBooking(id) {
 const b = myBookings.find(x => x.id === id);
 if (!b) { toast('Бронирование не найдено'); return; }
 try {
  await apiJson('/bookings/' + id, { method: 'DELETE' });
  await loadMyBookings();
  await loadAllBookings();
  renderAll();
  toast('Бронь отменена, слот снова свободен');
 } catch (err) {
  toast(apiErrorMessage(err));
 }
}

/* ---------- панель администратора ---------- */
function renderAdminRooms() {
 const target = $('adminRooms');
 if (!target) return;
 if ($('adminRoomCount')) $('adminRoomCount').textContent = rooms.length + ' помещений';

 target.innerHTML = rooms.length
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
          </td>
        </tr>`
     ).join('')
     : '<tr><td colspan="6" class="empty">Аудиторий пока нет.</td></tr>';
}

function renderAdmin() {
 if (!isAdmin()) return;

 if ($('adminRoomStat'))     $('adminRoomStat').textContent = rooms.length;
 if ($('adminTotal'))        $('adminTotal').textContent = allBookings.length;
 if ($('adminActive'))       $('adminActive').textContent = allBookings.filter(b => b.status === 'active').length;
 const buildings = [...new Set(rooms.map(r => r.building))].sort();
 if ($('adminBuildingCount')) $('adminBuildingCount').textContent = buildings.length;

 renderAdminRooms();

 const tbody = $('allBookings');
 if (!tbody) return;
 tbody.innerHTML = allBookings.length
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
     : '<tr><td colspan="6" class="empty">Бронирований пока нет.</td></tr>';
}

async function adminCancel(id) {
 if (!isAdmin()) { toast('403 · Только администратор'); return; }
 try {
  await apiJson('/admin/bookings/' + id, { method: 'DELETE' });
  await loadMyBookings();
  await loadAllBookings();
  renderAll();
  toast('Бронирование отменено администратором');
 } catch (err) {
  toast(apiErrorMessage(err));
 }
}

/* ---------- управление аудиториями (admin) ---------- */
function resetRoomForm() {
 editingRoomId = null;
 $('roomForm').reset();
 $('roomFloor').value = 1;
 $('roomFormTitle').textContent = 'Добавить аудиторию';
 $('saveRoomBtn').textContent = 'Добавить аудиторию';
 $('cancelRoomEdit').classList.add('hidden');
}

function editRoom(id) {
 if (!isAdmin()) { toast('403 · Только администратор'); return; }
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
 $('roomForm').scrollIntoView({ behavior: 'smooth', block: 'center' });
}

async function deleteRoom(id) {
 if (!isAdmin()) { toast('403 · Только администратор'); return; }
 const r = rooms.find(x => x.id === id);
 if (!r) return;
 if (!confirm(`Удалить аудиторию ${r.name}?`)) return;
 try {
  await apiJson('/admin/rooms/' + id, { method: 'DELETE' });
  await loadRooms();
  renderAll();
  toast('Аудитория удалена');
 } catch (err) {
  toast(apiErrorMessage(err));
 }
}

$('roomForm').addEventListener('submit', async e => {
 e.preventDefault();
 if (!isAdmin()) { toast('403 · Только администратор'); return; }

 const name = $('roomName').value.trim().toUpperCase();
 const building = $('roomBuilding').value;
 const floor = Number($('roomFloor').value);
 const capacity = Number($('roomCapacity').value);
 const desc = $('roomDesc').value.trim();
 const equipmentRu = $('roomEquipment').value.split(',').map(x => x.trim()).filter(Boolean);
 const equipment = equipmentRu.map(x => EQUIPMENT_FROM_RU[x.toLowerCase()] || x);

 if (!name || !['A', 'B', 'C', 'D'].includes(building) ||
     !Number.isInteger(floor) || floor < 1 ||
     !Number.isInteger(capacity) || capacity < 1) {
  toast('400 · Проверьте поля аудитории'); return;
 }

 const payload = { name, building, floor, capacity, equipment, desc };

 try {
  if (editingRoomId !== null) {
   await apiJson('/admin/rooms/' + editingRoomId, { method: 'PUT', body: JSON.stringify(payload) });
   toast('Изменения аудитории сохранены');
  } else {
   await apiJson('/admin/rooms', { method: 'POST', body: JSON.stringify(payload) });
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

/* ---------- статистика и сводки ---------- */
function updateBuildingStats() {
 const buildings = [...new Set(rooms.map(r => r.building))].sort();
 if ($('buildingCount'))      $('buildingCount').textContent = buildings.length;
 if ($('buildingList'))       $('buildingList').textContent = buildings.length ? buildings.join(', ') : 'Нет корпусов';
 if ($('adminBuildingCount')) $('adminBuildingCount').textContent = buildings.length;
 const heroNote = document.querySelector('.hero-note');
 if (heroNote) heroNote.textContent = rooms.length + ' пространств · ' + buildings.length + ' корпуса';
 const datePill = document.querySelector('#rooms .date-pill');
 if (datePill) datePill.textContent = rooms.length + ' аудиторий · ' + buildings.length + ' корпуса';
}

function updateStats() {
 const todayStr = dateISO(new Date());
 if ($('myCount'))    $('myCount').textContent = myBookings.filter(b => b.status === 'active').length;
 if ($('totalRooms')) $('totalRooms').textContent = rooms.length;
 if ($('freeRooms')) {
  if (isAdmin()) {
   const occupied = new Set(
       allBookings.filter(b => b.date === todayStr && b.status === 'active').map(b => b.roomId)
   );
   $('freeRooms').textContent = rooms.filter(r => !occupied.has(r.id)).length;
  } else {
   $('freeRooms').textContent = rooms.length;
  }
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
      const r = rooms.find(x => x.id === b.roomId) || { name: b.roomName || '—', building: '?' };
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

/* ---------- общий рендер и инициализация ---------- */
function renderAll() {
 updateBuildingStats();
 updateStats();
 renderRooms();
 renderFeatured();
 renderMyBookings();
 renderAdmin();
 renderUpcoming();
 setUser();
}

async function init() {
 $('todayLabel').textContent = new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'long', year: 'numeric' }).format(today);
 $('quickDate').value = dateISO(tomorrow);
 $('quickDate').min = dateISO(today);

 // 1. Аудитории доступны всем
 try { await loadRooms(); } catch { toast('Не удалось загрузить каталог аудиторий'); }

 // 2. Восстановление сессии (без тоста при 401)
 if (token) {
  try {
   const res = await fetch(API_BASE + '/auth/me', { headers: { Authorization: 'Bearer ' + token } });
   if (res.ok) {
    currentUserData = await res.json();
    currentUser = currentUserData.login;
   } else {
    setToken('');
   }
  } catch { setToken(''); }
 }

 // 3. Данные по роли
 if (!isGuest()) {
  try { await loadMyBookings(); } catch { /* 401 уже обработан */ }
  if (isAdmin()) {
   try { await loadAllBookings(); } catch { /* 403/401 */ }
  }
 }

 renderAll();
}

init();

/* Экспорт функций для inline-обработчиков в HTML */
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