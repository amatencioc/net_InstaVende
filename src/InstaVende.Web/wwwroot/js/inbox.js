var token = $('[name="__RequestVerificationToken"]').first().val();
var currentConvId = null;
var currentChannel = '';
var currentStatus = '';
var searchTerm = '';

var connection = new signalR.HubConnectionBuilder().withUrl('/inboxHub').withAutomaticReconnect().build();
connection.on('NewMessage', function(convId, message) {
    if (convId === currentConvId) appendMessage(message);
    loadConversations();
});
connection.on('ConversationUpdated', loadConversations);
connection.start().catch(console.error);

function loadConversations() {
    $.getJSON('/Inbox/GetConversations', { channel: currentChannel, status: currentStatus, search: searchTerm }, function(data) {
        var list = $('#conversationList').empty();
        data.forEach(function(c) {
            var badge = getChannelBadge(c.channelType);
            var statusCls = getStatusClass(c.status);
            var item = $('<div class="p-3 border-bottom conversation-item" style="cursor:pointer">' +
                '<div class="d-flex justify-content-between">' +
                '<strong>' + $('<span>').text(c.contactName || c.externalContactId).html() + '</strong>' +
                badge + '</div>' +
                '<div class="small text-truncate text-muted">' + $('<span>').text(c.lastMessage || '').html() + '</div>' +
                '<span class="badge ' + statusCls + ' mt-1">' + getStatusLabel(c.status) + '</span>' +
                '</div>');
            item.on('click', function() { openConversation(c.id, c.contactName || c.externalContactId, c.channelType, c.status); });
            if (c.id === currentConvId) item.addClass('bg-primary bg-opacity-10');
            list.append(item);
        });
    });
}

function openConversation(id, name, channelType, status) {
    currentConvId = id;
    $('#chatHeader, #chatInput').removeClass('d-none');
    $('#chatContactName').text(name);
    $('#chatChannelBadge').html(getChannelBadge(channelType)).removeClass('d-none');
    $('#chatStatusBadge').html('<span class="badge ' + getStatusClass(status) + '">' + getStatusLabel(status) + '</span>');
    $('#messageThread').empty();
    loadConversations();
    $.getJSON('/Inbox/GetMessages/' + id, function(msgs) { msgs.forEach(appendMessage); scrollBottom(); });
    connection.invoke('JoinConversation', id.toString()).catch(console.error);
}

function appendMessage(m) {
    var isOut = m.direction === 1;
    var align = isOut ? 'text-end' : 'text-start';
    var bg = isOut ? 'bg-primary text-white' : 'bg-white border';
    var html = '<div class="mb-2 ' + align + '"><div class="d-inline-block px-3 py-2 rounded ' + bg + '" style="max-width:75%">' +
        $('<span>').text(m.content).html() +
        '<div class="small opacity-75 mt-1">' + new Date(m.sentAt).toLocaleTimeString() + '</div></div></div>';
    $('#messageThread').append(html);
    scrollBottom();
}

function scrollBottom() { var el = document.getElementById('messageThread'); el.scrollTop = el.scrollHeight; }

$('#sendBtn').on('click', sendMessage);
$('#msgInput').on('keydown', function(e) { if (e.ctrlKey && e.key === 'Enter') sendMessage(); });

function sendMessage() {
    if (!currentConvId) return;
    var msg = $('#msgInput').val().trim();
    if (!msg) return;
    $.ajax({ url: '/Inbox/Send', method: 'POST', contentType: 'application/json',
        headers: { 'RequestVerificationToken': token },
        data: JSON.stringify({ conversationId: currentConvId, content: msg }),
        success: function() { $('#msgInput').val(''); },
        error: function() { alert('Error al enviar mensaje.'); }
    });
}

function setConvStatus(status) {
    if (!currentConvId) return;
    $.ajax({ url: '/Inbox/SetStatus', method: 'POST', contentType: 'application/json',
        headers: { 'RequestVerificationToken': token },
        data: JSON.stringify({ conversationId: currentConvId, status: status }),
        success: loadConversations, error: function() { alert('Error.'); }
    });
}

function getChannelBadge(ct) {
    if (ct === 1) return '<span class="badge bg-success"><i class="bi bi-whatsapp"></i></span>';
    if (ct === 2) return '<span class="badge bg-primary"><i class="bi bi-facebook"></i></span>';
    if (ct === 3) return '<span class="badge bg-danger"><i class="bi bi-instagram"></i></span>';
    return '';
}

function getStatusClass(s) {
    if (s === 1) return 'bg-info text-dark';
    if (s === 2) return 'bg-warning text-dark';
    if (s === 3) return 'bg-success';
    if (s === 4) return 'bg-secondary';
    return 'bg-light text-dark';
}

function getStatusLabel(s) {
    if (s === 1) return 'Bot';
    if (s === 2) return 'Esperando';
    if (s === 3) return 'Humano';
    if (s === 4) return 'Resuelto';
    return '';
}

$('[data-channel]').on('click', function() {
    $('[data-channel]').removeClass('active');
    $(this).addClass('active');
    currentChannel = $(this).data('channel');
    loadConversations();
});

$('#statusFilter').on('change', function() { currentStatus = $(this).val(); loadConversations(); });
$('#inboxSearch').on('input', function() { searchTerm = $(this).val(); loadConversations(); });

$(loadConversations);
