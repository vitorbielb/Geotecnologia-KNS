function selectfile(el) {
    var file = el.parent().parent().parent().find('.file-upload-default');
    file.trigger('click');
}

function setfilename(el) {
    el.parent().find('.form-control').val(el.val().replace(/C:\\fakepath\\/i, ''));
}

function uploadfile(vinculoId) {
    const file = $('.file-upload-default')[0].files[0];
    readFileAsByteArray(file).then(byteArray => {
        var formData = new FormData();
        formData.append('vinculoId', vinculoId);
        formData.append('Descricao', file.name);
        formData.append('ContentType', file.type);
        formData.append('Dados', byteArray);
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
                let errors = response.responseJSON.errors;

                Object.keys(errors).forEach(function (field) {
                    let errorMessages = errors[field];
                    errorMessages.forEach(function (errorMessage) {
                        $('#file-upload-error').html(errorMessage);
                    });
                });
            }
        });
    });
}

function readFileAsByteArray(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();

        reader.onload = function (event) {
            const arrayBuffer = event.target.result;
            const byteArray = new Uint8Array(arrayBuffer);

            resolve(byteArray);
        };

        reader.onerror = function (event) {
            reject(event.target.error);
        };

        reader.readAsArrayBuffer(file);
    });
}

function setdeletefilemodal(el) {
    const modal = $('#deletefileModal');
    const file = el.parent().parent().children()[0];
    modal.find('.modal-body').html(file.innerHTML);
    modal.find('.btn-danger').click(function () {
        modal.find()
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

//function viewFile(el) {
//    const file = el.parent().parent().children()[0];
//    const id = $(file).find('.hidden').val();
//    $.ajax({
//        type: 'POST',
//        url: '../ViewFile?id=' + id
//    });
//}