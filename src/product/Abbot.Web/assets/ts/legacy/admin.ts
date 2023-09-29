// Set up image uploads
export function adminOnLoad() {
    function ensureElement(id) {
        const element = document.getElementById(id);
        if (!element) {
            console.log(`The element with id '${id}' is missing on this page.`);
        }
        return element;
    }
    
    document.querySelectorAll('input.js-file-input').forEach(uploadInput => {
        if (!(uploadInput instanceof HTMLInputElement)) {
            return;
        }

        const uploadInputId = uploadInput.id;
        
        if (!uploadInputId) {
            console.log(`Every upload input must have an Id.`);
            return;
        }
        const form = uploadInput.form;
        if (!form) {
            console.log(`The upload input ${uploadInputId} must be in a form.`);
            return;
        }
        
        // When a user clicks the file input to choose a file, and then selects 
        // a file, this element is updated with the file name.
        const fileNameLabel = ensureElement(`${uploadInputId}-file-name`);
        if (!fileNameLabel) {
            return;
        }
        const errorElement = ensureElement(`${uploadInputId}-error`);
        if (!errorElement) {
            return;
        }

        const saveButton = ensureElement(`${uploadInputId}-save-button`);
        if (!saveButton) {
            return;
        }

        uploadInput.onchange = async () => {
            // When a user changes the upload input, we upload the file they select and 
            // then update the preview image and displayed file name.
            // We also update a hidden input with a checksum returned by the upload endpoint.
            // When the user clicks the Save Button, we send the hidden input with the checksum 
            // to make sure they didn't muck with the form and that the image URL they're sending 
            // matches the image they uploaded. That's what the hidden input checksum is for.
            const isImage = uploadInput.classList.contains('image-upload');
            const uploadFile = uploadInput.files[0];
            const uploadType = isImage ? "image" : "file";
            const uploadTypeWithIndefiniteArticle = isImage ? "an image" : "a file";
            if (uploadFile.size === 0) {
                errorElement.innerText = `Please choose a non-empty ${uploadType}.`;
                return;
            }
            if (uploadFile.size > 50000) {
                errorElement.innerText = `Please choose ${uploadTypeWithIndefiniteArticle} 50kb or smaller.`;
                return;
            }
            
            if (uploadInput.classList.contains('image-upload')) {
                if (!(uploadFile.name.endsWith('.png') || uploadFile.name.endsWith('.jpg') || uploadFile.name.endsWith('.jpeg'))) {
                    errorElement.innerText = 'Only png, jpg, and jpeg files are allowed.';
                    return;
                }
            }
            fileNameLabel.textContent = uploadFile.name;
            const formData = new FormData(form);
            try {
                const response = await fetch(form.action, {
                    method: 'POST',
                    body: formData
                });
                if (response.ok) {
                    const result = await response.json();
                    if (result) {
                        if (result.url) {
                            const currentFile = ensureElement(`${uploadInputId}-current`);
                            if (!currentFile || !(currentFile instanceof HTMLImageElement)) {
                                return;
                            }
                            const uploadHiddenInput = ensureElement(`${uploadInputId}-hidden-input`);
                            if (!uploadHiddenInput || !(uploadHiddenInput instanceof HTMLInputElement)) {
                                return;
                            }
                            const uploadHiddenInputChecksum = ensureElement(`${uploadInputId}-hidden-input-checksum`);
                            if (!uploadHiddenInputChecksum || !(uploadHiddenInputChecksum instanceof HTMLInputElement)) {
                                return;
                            }
                            errorElement.innerText = null;
                            currentFile.src = result.url;
                            uploadHiddenInput.value = result.url;
                            uploadHiddenInputChecksum.value = result.checksum;
                            currentFile.classList.add('temp');
                            if (saveButton instanceof HTMLInputElement || saveButton instanceof HTMLButtonElement) {
                                saveButton.disabled = false;
                            }
                        }
                        else if (result.error) {
                            errorElement.innerText = result.error;
                        }
                    }
                }
            } catch (error) {
                alert('Error uploading file. ' + error);
                if (saveButton instanceof HTMLInputElement || saveButton instanceof HTMLButtonElement) {
                    saveButton.disabled = true;
                }
            }
        }
    });
}