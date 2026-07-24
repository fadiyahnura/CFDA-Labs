import axios from 'axios';
import './style.css';

interface Task {
  id: number;
  title: string;
  isCompleted: boolean;
}

const API_URL = 'http://localhost:5000/api/tasks';
let tasks: Task[] = [];

const appDiv = document.querySelector<HTMLDivElement>('#app');

if (appDiv) {
  // 1. Render Struktur UI (Form Tambah & Container List)
  appDiv.innerHTML = `
    <div style="padding: 20px; font-family: system-ui, sans-serif; max-width: 500px; margin: 0 auto; color: var(--text);">
      <h2>📋 Task Manager (CRUD Latihan)</h2>
      <hr style="border-color: var(--border);" />
      
      <!-- Form Tambah Task -->
      <form id="task-form" style="display: flex; gap: 10px; margin-bottom: 20px;">
        <input 
          type="text" 
          id="task-input" 
          placeholder="Tulis tugas baru..." 
          required 
          style="flex: 1; padding: 10px; border: 1px solid var(--border); border-radius: 6px; background: var(--bg); color: var(--text);"
        />
        <button type="submit" style="padding: 10px 16px; background: var(--accent); color: white; border: none; border-radius: 6px; cursor: pointer; font-weight: bold;">
          Tambah
        </button>
      </form>

      <div id="loading-status" style="margin-bottom: 10px; font-weight: 500;">Sedang memuat data...</div>
      <ul id="task-list" style="list-style-type: none; padding: 0;"></ul>
    </div>
  `;

  const taskForm = document.getElementById('task-form') as HTMLFormElement;
  const taskInput = document.getElementById('task-input') as HTMLInputElement;
  const taskListUl = document.getElementById('task-list') as HTMLUListElement;
  const loadingStatusDiv = document.getElementById('loading-status') as HTMLDivElement;

  // 2. Fungsi Utama untuk Render Data ke Elemen HTML (Read)
  const renderTasks = () => {
    taskListUl.innerHTML = '';
    if (tasks.length === 0) {
      taskListUl.innerHTML = '<p style="color: #888;">Tidak ada tugas saat ini.</p>';
      return;
    }

    tasks.forEach(task => {
      const li = document.createElement('li');
      li.style.padding = '12px';
      li.style.border = '1px solid var(--border)';
      li.style.borderRadius = '6px';
      li.style.marginBottom = '10px';
      li.style.display = 'flex';
      li.style.justifyContent = 'space-between';
      li.style.alignItems = 'center';
      li.style.backgroundColor = task.isCompleted ? 'rgba(170, 59, 255, 0.1)' : 'var(--bg)';

      // Konten Teks (Bisa diklik untuk Update / Toggle Complete)
      const textSpan = document.createElement('span');
      textSpan.textContent = task.title;
      textSpan.style.cursor = 'pointer';
      textSpan.style.flex = '1';
      textSpan.style.textDecoration = task.isCompleted ? 'line-through' : 'none';
      textSpan.style.color = task.isCompleted ? '#888' : 'var(--text)';
      textSpan.addEventListener('click', () => toggleTask(task.id));

      // Tombol Hapus (Delete)
      const deleteBtn = document.createElement('button');
      deleteBtn.textContent = '❌';
      deleteBtn.style.background = 'none';
      deleteBtn.style.border = 'none';
      deleteBtn.style.cursor = 'pointer';
      deleteBtn.style.padding = '4px 8px';
      deleteBtn.addEventListener('click', (e) => {
        e.stopPropagation(); // Mencegah kepicu klik teks
        deleteTask(task.id);
      });

      li.appendChild(textSpan);
      li.appendChild(deleteBtn);
      taskListUl.appendChild(li);
    });
  };

  // 3. Fungsi Load Data Awal (Menggunakan API dengan Fallback LocalStorage)
  const loadTasks = async () => {
    try {
      const response = await axios.get<Task[]>(API_URL);
      tasks = response.data;
      loadingStatusDiv.style.color = '#28a745';
      loadingStatusDiv.textContent = '🟢 Connected ke Backend!';
    } catch (error) {
      console.log('Backend mati, beralih menggunakan LocalStorage sebagai demo latihan.');
      const localData = localStorage.getItem('latihan_tasks');
      tasks = localData ? JSON.parse(localData) : [
        { id: 1, title: 'Latihan setup React & Vite', isCompleted: true },
        { id: 2, title: 'Latihan CRUD TypeScript murni', isCompleted: false }
      ];
      loadingStatusDiv.style.color = '#ffc107';
      loadingStatusDiv.textContent = '🟡 Demo Mode (Backend Offline - Menggunakan LocalStorage)';
    }
    renderTasks();
  };

  // Save data cadangan ke localstorage biar ga ilang pas di-refresh kalau demo offline
  const saveToLocalBackup = () => {
    localStorage.setItem('latihan_tasks', JSON.stringify(tasks));
  };

  // 4. Aksi Jaringan: Tambah Data (Create)
  taskForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    const title = taskInput.value.trim();
    if (!title) return;

    const newTaskData = { title, isCompleted: false };

    try {
      const response = await axios.post<Task>(API_URL, newTaskData);
      tasks.push(response.data);
    } catch (error) {
      // Jalankan logika offline jika backend tidak aktif
      const mockTask: Task = { id: Date.now(), ...newTaskData };
      tasks.push(mockTask);
      saveToLocalBackup();
    }

    taskInput.value = '';
    renderTasks();
  });

  // 5. Aksi Jaringan: Ubah Status Ceklis (Update)
  const toggleTask = async (id: number) => {
    const task = tasks.find(t => t.id === id);
    if (!task) return;

    const updatedData = { ...task, isCompleted: !task.isCompleted };

    try {
      await axios.put(`${API_URL}/${id}`, updatedData);
      task.isCompleted = !task.isCompleted;
    } catch (error) {
      task.isCompleted = !task.isCompleted;
      saveToLocalBackup();
    }
    renderTasks();
  };

  // 6. Aksi Jaringan: Hapus Data (Delete)
  const deleteTask = async (id: number) => {
    try {
      await axios.delete(`${API_URL}/${id}`);
      tasks = tasks.filter(t => t.id !== id);
    } catch (error) {
      tasks = tasks.filter(t => t.id !== id);
      saveToLocalBackup();
    }
    renderTasks();
  };

  // Jalankan load data saat inisialisasi awal
  loadTasks();
}