function selectFile(fileField) {
    // Create an input element of type 'file'
    var fileInput = document.createElement('input');
    fileInput.type = 'file';

    // Set an event listener to capture the file selection
    fileInput.addEventListener('change', function () {
        if (fileInput.files.length > 0) {
            // Get the selected file
            var selectedFile = fileInput.files[0];

            // Update the input element's value with the file name
            inputElement.value = selectedFile.name;

            // You can also update other fields with file information here if needed
            // For example, if you have a div with id "file-info" to display file info:
            // var fileInfoElement = document.getElementById('file-info');
            // fileInfoElement.innerHTML = 'File Name: ' + selectedFile.name + '<br>File Size: ' + selectedFile.size + ' bytes';
        }
    });

    // Trigger a click event on the file input to open the file selection dialog
    fileInput.click();
}