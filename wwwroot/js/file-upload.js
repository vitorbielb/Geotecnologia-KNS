(function ($) {
    'use strict';
    $(function () {
        $('.file-upload-browse').on('click', function () {
            var file = $(this).parent().parent().parent().find('.file-upload-default');
            file.trigger('click');
        });
        $('.file-upload-default').on('change', function () {
            $(this).parent().find('.form-control').val($(this).val().replace(/C:\\fakepath\\/i, ''));
        });
    });
})(jQuery);

function uploadfile(vinculoId) {
    //const fileInput = $('.file-upload-default')[0];
    // Check if the file input element exists
    //if (fileInput) {
    // Check if files have been selected
    //if (fileInput.files.length > 0) {
    // Get the first selected file
    //const file = fileInput.files[0];

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