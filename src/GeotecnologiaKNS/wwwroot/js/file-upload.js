// Limite espelhado em ArquivoUpload.TamanhoMaximoEmBytes (servidor).
const TAMANHO_MAXIMO_ARQUIVO = 10 * 1024 * 1024;

function selectfile(el) {
    var file = el.parent().parent().parent().find('.file-upload-default');
    file.trigger('click');
}

function setfilename(el) {
    el.parent().find('.form-control').val(el.val().replace(/C:\\fakepath\\/i, ''));
}

function uploadfile(vinculoId) {
    enviarArquivo(vinculoId);
}

function uploadCartografiafile(vinculoId, tipo) {
    enviarArquivo(vinculoId, tipo);
}

function enviarArquivo(vinculoId, tipo) {
    const input = $('.file-upload-default')[0];
    const file = input && input.files ? input.files[0] : null;

    if (!file) {
        $('#file-upload-error').html('Selecione um arquivo.');
        return;
    }

    if (file.size > TAMANHO_MAXIMO_ARQUIVO) {
        $('#file-upload-error').html('O arquivo excede o limite de 10 MB.');
        return;
    }

    $('#file-upload-error').html('');

    // O arquivo vai como multipart; enviar os bytes em texto inflava o corpo
    // da requisição em cerca de 4x e exigia parsing manual no servidor.
    const formData = new FormData();
    formData.append('vinculoId', vinculoId);
    formData.append('Descricao', file.name);
    formData.append('ContentType', file.type);
    formData.append('Dados', file, file.name);

    if (tipo) {
        formData.append('Tipo', tipo);
    }

    $.ajax({
        type: 'POST',
        url: '../Upload',
        data: formData,
        contentType: false,
        processData: false,
        success: function (response) {
            $('#file-upload-content').html(response);
        },
        error: function (response) {
            exibirErros(response);
        }
    });
}

function exibirErros(response) {
    const errors = response && response.responseJSON ? response.responseJSON.errors : null;

    if (!errors) {
        $('#file-upload-error').html('Não foi possível enviar o arquivo.');
        return;
    }

    const mensagens = [];

    Object.keys(errors).forEach(function (field) {
        errors[field].forEach(function (errorMessage) {
            mensagens.push(errorMessage);
        });
    });

    $('#file-upload-error').html(mensagens.join('<br />'));
}

function setdeletefilemodal(el) {
    const modal = $('#deletefileModal');
    const file = el.parent().parent().children()[0];
    modal.find('.modal-body').html(file.innerHTML);
    modal.find('.btn-danger').click(function () {
        const id = $(file).find('.hidden').val();
        $.ajax({
            type: 'POST',
            url: '../DeleteFile?id=' + id,
            success: function (response) {
                $(document).find('.modal-backdrop').remove();
                $('#file-upload-content').html(response);
            }
        });
    });
}
