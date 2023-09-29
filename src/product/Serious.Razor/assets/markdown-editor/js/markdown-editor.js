export default function setupMarkdownTextAreas(document) {
    document.querySelectorAll('tab-container').forEach(tabContainer => {
        if (tabContainer.dataset.markdownEditor) {
            return;
        }
        tabContainer.dataset.markdownEditor = "true";
        const preview = tabContainer.querySelector('.preview-content');
        const emptyPreviewMessage = preview.innerHTML;
        const previewUrl = tabContainer.dataset.previewUrl;
        const textarea = tabContainer.querySelector('textarea');

        tabContainer.addEventListener('tab-container-changed', async evt => {
            tabContainer.dataset.selectedTab = evt.detail.relatedTarget.dataset.tab;
            const markdown = textarea.value;
            if (tabContainer.dataset.selectedTab === 'preview') {
                if (markdown.length > 0) {
                    const response = await fetch(previewUrl, {
                        method: 'POST',
                        headers: {
                            'Accept': 'application/json',
                            'Content-Type': 'application/json'
                        },
                        body: JSON.stringify(markdown)
                    });
                    if (response.ok) {
                        preview.innerHTML = await response.text();
                    }
                    else {
                        preview.innerHTML = 'Sorry, there was a problem fetching the preview.'
                    }

                } else {
                    preview.innerHTML = emptyPreviewMessage
                }
            }
        })
    });
}
