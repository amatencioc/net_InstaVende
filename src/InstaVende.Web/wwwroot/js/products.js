var token = $('[name="__RequestVerificationToken"]').first().val();
var currentSearch = '', currentCategory = '';

function loadProducts() {
    $.getJSON('/Products/GetAll', { search: currentSearch, categoryId: currentCategory }, function(data) {
        var grid = $('#productGrid').empty();
        if (!data.length) { grid.html('<div class="col-12 text-muted">No se encontraron productos.</div>'); return; }
        data.forEach(function(p) {
            grid.append(
                '<div class="col-md-3 col-sm-6">' +
                '<div class="card h-100">' +
                (p.imageUrl ? '<img src="' + p.imageUrl + '" class="card-img-top" style="height:160px;object-fit:cover">' : '<div class="bg-secondary text-white d-flex align-items-center justify-content-center" style="height:160px"><i class="bi bi-image fs-1"></i></div>') +
                '<div class="card-body">' +
                '<h6 class="card-title">' + p.name + '</h6>' +
                '<p class="card-text text-primary fw-bold">$' + p.price.toFixed(2) + '</p>' +
                '<p class="card-text small text-muted">Stock: ' + p.stock + '</p>' +
                (p.isActive ? '<span class="badge bg-success">Activo</span>' : '<span class="badge bg-secondary">Inactivo</span>') +
                (p.isFeatured ? ' <span class="badge bg-warning text-dark">Destacado</span>' : '') +
                '</div>' +
                '<div class="card-footer d-flex gap-1">' +
                '<a href="/Products/Edit/' + p.id + '" class="btn btn-sm btn-outline-primary flex-fill"><i class="bi bi-pencil"></i></a>' +
                '<button class="btn btn-sm btn-outline-danger flex-fill" onclick="deleteProduct(' + p.id + ')"><i class="bi bi-trash"></i></button>' +
                '</div></div></div>'
            );
        });
    });
}

function deleteProduct(id) {
    if (!confirm('¿Eliminar este producto?')) return;
    $.ajax({ url: '/Products/Delete/' + id, method: 'POST', headers: { 'RequestVerificationToken': token },
        success: loadProducts, error: function() { alert('Error al eliminar.'); }
    });
}

$('#searchInput').on('input', function() { currentSearch = $(this).val(); loadProducts(); });
$('#categoryFilter').on('change', function() { currentCategory = $(this).val(); loadProducts(); });

$('#formCategory').on('submit', function(e) {
    e.preventDefault();
    $.ajax({ url: '/Products/SaveCategory', method: 'POST', contentType: 'application/json',
        headers: { 'RequestVerificationToken': token },
        data: JSON.stringify({ id: parseInt($('#catId').val()), name: $('#catName').val(), description: $('#catDesc').val() }),
        success: function() { bootstrap.Modal.getInstance(document.getElementById('modalCategory')).hide(); location.reload(); },
        error: function() { alert('Error al guardar categoría.'); }
    });
});

$(loadProducts);
