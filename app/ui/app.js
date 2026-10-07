const { apiUrl, appToken } = window.config;

let adminToken = null;

const $ = (id) => document.getElementById(id);

function notify(message, isError = false) {
  const el = $('message');
  el.textContent = message;
  el.className = isError ? 'error' : '';
}

function show(view) {
  for (const section of document.querySelectorAll('main > section')) {
    section.hidden = section.id !== `view-${view}`;
  }
  $('logout').hidden = view === 'login';
}

async function api(path, { method = 'GET', body, admin = false } = {}) {
  const headers = { 'X-App-Token': appToken };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (admin) headers['X-Admin-Token'] = adminToken;

  let res;
  try {
    res = await fetch(apiUrl + path, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new Error('Sunucuya ulaşılamadı.');
  }
  if (res.status === 204) return null;
  const data = await res.json().catch(() => ({}));
  if (!res.ok) throw new Error(data.error || `İşlem başarısız (${res.status}).`);
  return data;
}

function cell(text) {
  const td = document.createElement('td');
  td.textContent = text;
  return td;
}

function actionButton(text, onClick, cls = '') {
  const button = document.createElement('button');
  button.type = 'button';
  button.textContent = text;
  if (cls) button.className = cls;
  button.addEventListener('click', onClick);
  return button;
}

function resetSession() {
  adminToken = null;
  $('login-form').reset();
  notify('');
  $('adopter-form').reset();
  $('adoptee-form').reset();
  $('pet-list').replaceChildren();
  show('login');
}

$('logout').addEventListener('click', resetSession);

$('login-form').addEventListener('submit', async (event) => {
  event.preventDefault();
  const role = new FormData(event.target).get('role');
  const name = $('username').value.trim();
  try {
    if (role === 'admin') {
      const { token } = await api('/api/login', {
        method: 'POST',
        body: { username: name, password: $('password').value },
      });
      adminToken = token;
      await loadAdmin();
      show('admin');
    } else {
      if (!name) throw new Error('Lütfen Ad Soyad bilgisini giriniz!');
      if (role === 'adopter') {
        $('adopter-name').value = name;
        show('adopter');
      } else {
        $('adoptee-name').value = name;
        await loadApproved();
        show('adoptee');
      }
    }
    notify('');
  } catch (err) {
    notify(err.message, true);
  }
});

$('adopter-form').addEventListener('submit', async (event) => {
  event.preventDefault();
  const form = event.target;
  const data = Object.fromEntries(new FormData(form));
  try {
    await api('/api/pets', {
      method: 'POST',
      body: { ...data, age: data.age === '' ? null : Number(data.age) },
    });
    notify('Bilgiler başarıyla kaydedildi!');
    for (const name of ['city', 'phone', 'petName', 'petType', 'breed', 'age']) {
      form.elements[name].value = '';
    }
  } catch (err) {
    notify(err.message, true);
  }
});

function petLabel(p) {
  return `İsim Soyisim: ${p.ownerName}, Şehir: ${p.city}, Pet İsmi: ${p.petName}, Tür: ${p.petType}, Cins: ${p.breed}`;
}

async function loadApproved() {
  const pets = await api('/api/pets/approved');
  const box = $('pet-list');
  box.replaceChildren(
    ...pets.map((p) => {
      const label = document.createElement('label');
      const checkbox = document.createElement('input');
      checkbox.type = 'checkbox';
      checkbox.value = p.id;
      label.append(checkbox, ` ${petLabel(p)}`);
      return label;
    })
  );
  if (pets.length === 0) box.textContent = 'Şu an sahiplenilebilecek hayvan yok.';
}

$('refresh').addEventListener('click', async () => {
  try {
    await loadApproved();
    notify('');
  } catch (err) {
    notify(err.message, true);
  }
});

$('adoptee-form').addEventListener('submit', async (event) => {
  event.preventDefault();
  const form = event.target;
  const data = Object.fromEntries(new FormData(form));
  const checked = [...$('pet-list').querySelectorAll('input:checked')];
  const petIds = checked.map((c) => Number(c.value));
  try {
    await api('/api/adoption-requests', { method: 'POST', body: { ...data, petIds } });
    notify('Kayıt başarıyla kaydedildi!');
    form.elements.phone.value = '';
    form.elements.city.value = '';
    for (const c of checked) c.checked = false;
  } catch (err) {
    notify(err.message, true);
  }
});

async function act(path, method, okMessage) {
  try {
    await api(path, { method, admin: true });
    notify(okMessage);
    await loadAdmin();
  } catch (err) {
    notify(err.message, true);
  }
}

function fillRows(tbody, rows, makeRow, emptyText, columns) {
  if (rows.length === 0) {
    const tr = document.createElement('tr');
    const td = cell(emptyText);
    td.colSpan = columns;
    tr.append(td);
    tbody.replaceChildren(tr);
    return;
  }
  tbody.replaceChildren(...rows.map(makeRow));
}

function renderPending(pets) {
  fillRows($('pending-body'), pets, (p) => {
    const tr = document.createElement('tr');
    const actions = document.createElement('td');
    actions.append(
      actionButton('Onayla', () => act(`/api/admin/pets/${p.id}/approve`, 'POST', 'Kayıt onaylandı ve sahiplendirilmeye hazır!')),
      actionButton('Reddet', () => act(`/api/admin/pets/${p.id}`, 'DELETE', 'Kayıt başarıyla silindi!'), 'secondary')
    );
    tr.append(
      cell(p.ownerName), cell(p.city), cell(p.phone), cell(p.petName),
      cell(p.petType), cell(p.breed), cell(p.age), cell(p.neutered), actions
    );
    return tr;
  }, 'Bekleyen kayıt yok.', 9);
}

function renderRequests(requests) {
  fillRows($('requests-body'), requests, (r) => {
    const tr = document.createElement('tr');
    const actions = document.createElement('td');
    actions.append(
      actionButton('Kabul', () => {
        if (!confirm('Bu hayvanı sahiplendirmek ve tüm tablolardan silmek istediğinize emin misiniz?')) return;
        act(`/api/admin/adoption-requests/${r.id}/accept`, 'POST', 'Kayıt sahiplendirildi ve tüm tablolardan kaldırıldı.');
      }),
      actionButton('Reddet', () => act(`/api/admin/adoption-requests/${r.id}`, 'DELETE', 'Kayıt reddedildi ve listeden kaldırıldı.'), 'secondary')
    );
    tr.append(cell(r.fullName), cell(r.phone), cell(r.city), cell(`${r.petName} (${r.petType}, ${r.breed})`), actions);
    return tr;
  }, 'Bekleyen sahiplenme isteği yok.', 5);
}

function renderChart(stats) {
  const chart = $('chart');
  const max = Math.max(1, ...stats.map((s) => s.count));
  chart.replaceChildren(
    ...stats.map((s) => {
      const row = document.createElement('div');
      row.className = 'bar-row';
      const label = document.createElement('span');
      label.textContent = s.petType;
      const bar = document.createElement('div');
      bar.className = 'bar';
      bar.style.width = `${(s.count / max) * 100}%`;
      const count = document.createElement('strong');
      count.textContent = s.count;
      row.append(label, bar, count);
      return row;
    })
  );
  if (stats.length === 0) chart.textContent = 'Henüz onaylı hayvan yok.';
}

async function loadAdmin() {
  const [pets, requests, stats] = await Promise.all([
    api('/api/admin/pets/pending', { admin: true }),
    api('/api/admin/adoption-requests', { admin: true }),
    api('/api/admin/stats', { admin: true }),
  ]);
  renderPending(pets);
  renderRequests(requests);
  renderChart(stats);
}
