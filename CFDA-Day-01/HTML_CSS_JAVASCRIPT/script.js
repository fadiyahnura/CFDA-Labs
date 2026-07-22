// Menggunakan const untuk elemen DOM yang sifatnya tetap (tidak di-reassign)
const taskInput = document.getElementById('task-input');
const addBtn = document.getElementById('add-btn');
const taskList = document.getElementById('task-list');
const totalCount = document.getElementById('total-count');

// Menggunakan let karena nilai counter akan berubah-ubah (dinamis)
let taskCount = 0;

// Fungsi untuk memperbarui jumlah total tugas di layar
function updateDOMCount() {
    totalCount.textContent = taskCount;
}

// Fungsi utama untuk menambahkan tugas baru
function addTask() {
    const taskText = taskInput.value.trim();

    // Validasi sederhana agar tidak bisa memasukkan teks kosong
    if (taskText === '') {
        alert('Tuliskan tugas terlebih dahulu!');
        return;
    }

    // Pembuatan elemen komponen li baru secara dinamis
    const li = document.createElement('li');
    li.className = 'task-item';
    
    // Memasukkan teks tugas ke dalam elemen
    li.innerHTML = `
        <span>${taskText}</span>
        <button class="delete-btn">Hapus</button>
    `;

    // Logika hapus data ketika tombol hapus di dalam list diklik
    li.querySelector('.delete-btn').addEventListener('click', function() {
        li.remove();
        taskCount--; // Mengurangi counter
        updateDOMCount();
    });

    // Masukkan elemen baru ke dalam list utama di HTML
    taskList.appendChild(li);
    
    // Perbarui counter
    taskCount++;
    updateDOMCount();

    // Kosongkan kembali kolom input setelah berhasil menambah data
    taskInput.value = '';
    taskInput.focus();
}

// Event trigger saat tombol tambah diklik
addBtn.addEventListener('click', addTask);

// Event trigger saat menekan tombol 'Enter' pada keyboard di kolom input
taskInput.addEventListener('keypress', function(e) {
    if (e.key === 'Enter') {
        addTask();
    }
});