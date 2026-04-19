var token = $('[name="__RequestVerificationToken"]').first().val();

// Config form
$('#formConfig').on('submit', function(e) {
    e.preventDefault();
    var data = {};
    $(this).serializeArray().forEach(function(f) { data[f.name] = f.value; });
    data.EnableHandoff = $(this).find('[name=EnableHandoff]').is(':checked');
    data.IsActive = $(this).find('[name=IsActive]').is(':checked');
    data.InteractionLevel = parseInt(data.InteractionLevel);
    $.ajax({ url: '/Bot/SaveConfig', method: 'POST', contentType: 'application/json',
        headers: { 'RequestVerificationToken': token }, data: JSON.stringify(data),
        success: function() { alert('Configuración guardada.'); },
        error: function() { alert('Error al guardar.'); }
    });
});

// Intent modal
function openIntentModal(id) {
    $('#intentId').val(id);
    if (id > 0) {
        $.getJSON('/Bot/GetIntent/' + id, function(d) {
            $('#intentName').val(d.intentName);
            $('#intentPhrases').val(d.triggerPhrases);
            $('#intentResponse').val(d.response);
            $('#intentPriority').val(d.priority);
            $('#intentActive').prop('checked', d.isActive);
        });
    } else {
        $('#formIntent')[0].reset();
        $('#intentId').val(0);
    }
    new bootstrap.Modal(document.getElementById('modalIntent')).show();
}

$('#formIntent').on('submit', function(e) {
    e.preventDefault();
    var data = {
        id: parseInt($('#intentId').val()),
        intentName: $('#intentName').val(),
        triggerPhrases: $('#intentPhrases').val(),
        response: $('#intentResponse').val(),
        priority: parseInt($('#intentPriority').val()) || 0,
        isActive: $('#intentActive').is(':checked')
    };
    $.ajax({ url: '/Bot/SaveIntent', method: 'POST', contentType: 'application/json',
        headers: { 'RequestVerificationToken': token }, data: JSON.stringify(data),
        success: function() { location.reload(); },
        error: function() { alert('Error al guardar intención.'); }
    });
});

function deleteIntent(id) {
    if (!confirm('¿Eliminar esta intención?')) return;
    $.ajax({ url: '/Bot/DeleteIntent/' + id, method: 'POST', headers: { 'RequestVerificationToken': token },
        success: function() { location.reload(); }, error: function() { alert('Error al eliminar.'); }
    });
}

// Knowledge modal
function openKbModal(id) {
    $('#kbId').val(id);
    if (id > 0) {
        $.getJSON('/Bot/GetKnowledge/' + id, function(d) {
            $('#kbQuestion').val(d.question);
            $('#kbAnswer').val(d.answer);
            $('#kbCategory').val(d.category);
            $('#kbActive').prop('checked', d.isActive);
        });
    } else {
        $('#formKb')[0].reset();
        $('#kbId').val(0);
    }
    new bootstrap.Modal(document.getElementById('modalKb')).show();
}

$('#formKb').on('submit', function(e) {
    e.preventDefault();
    var data = {
        id: parseInt($('#kbId').val()),
        question: $('#kbQuestion').val(),
        answer: $('#kbAnswer').val(),
        category: $('#kbCategory').val(),
        isActive: $('#kbActive').is(':checked')
    };
    $.ajax({ url: '/Bot/SaveKnowledge', method: 'POST', contentType: 'application/json',
        headers: { 'RequestVerificationToken': token }, data: JSON.stringify(data),
        success: function() { location.reload(); },
        error: function() { alert('Error al guardar.'); }
    });
});

function deleteKb(id) {
    if (!confirm('¿Eliminar esta entrada?')) return;
    $.ajax({ url: '/Bot/DeleteKnowledge/' + id, method: 'POST', headers: { 'RequestVerificationToken': token },
        success: function() { location.reload(); }, error: function() { alert('Error al eliminar.'); }
    });
}

// Bot preview
$('#previewSend').on('click', sendPreview);
$('#previewInput').on('keypress', function(e) { if (e.key === 'Enter') sendPreview(); });

function sendPreview() {
    var msg = $('#previewInput').val().trim();
    if (!msg) return;
    appendMessage('user', msg);
    $('#previewInput').val('');
    $.ajax({ url: '/Bot/Preview', method: 'POST', contentType: 'application/json',
        headers: { 'RequestVerificationToken': token }, data: JSON.stringify({ message: msg }),
        success: function(r) { appendMessage('bot', r.response); },
        error: function() { appendMessage('bot', 'Error al conectar con el bot.'); }
    });
}

function appendMessage(role, text) {
    var align = role === 'user' ? 'text-end' : 'text-start';
    var bg = role === 'user' ? 'bg-primary text-white' : 'bg-white border';
    var html = '<div class="mb-2 ' + align + '"><span class="d-inline-block px-3 py-2 rounded ' + bg + '" style="max-width:80%">' + $('<div>').text(text).html() + '</span></div>';
    $('#previewMessages').append(html).scrollTop($('#previewMessages')[0].scrollHeight);
}
