function FileSaveAs(filename, fileEncoding, fileContent) {
    var link = document.createElement('a');
    link.download = filename;
    link.href = fileEncoding + encodeURIComponent(fileContent)
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}