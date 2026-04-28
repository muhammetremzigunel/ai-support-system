function openEdit(id, category, title, content) {
    document.getElementById('editId').value = id;
    document.getElementById('editCategory').value = category;
    document.getElementById('editTitle').value = title;
    document.getElementById('editContent').value = content;

    // Use native <dialog> API instead of Bootstrap Modal
    var dialog = document.getElementById('editModal');
    dialog.showModal();
}