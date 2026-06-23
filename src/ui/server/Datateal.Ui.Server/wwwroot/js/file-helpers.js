// File I/O and DOM click helpers used by pages and DataFrameView.

window.openFileAsText = function (inputElement) {
  return new Promise((resolve, reject) => {
    const file = inputElement.files[0];
    if (!file) {
      reject('No file selected');
      return;
    }
    const reader = new FileReader();
    reader.onload = (e) => resolve(e.target.result);
    reader.onerror = () => reject('Failed to read file');
    reader.readAsText(file);
  });
};

window.downloadFileBytes = function (filename, bytes, mimeType) {
  const blob = new Blob([new Uint8Array(bytes)], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
};

window.downloadFile = function (filename, content) {
  const blob = new Blob([content], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
};

window.clickElement = function (element) {
  element.click();
};
