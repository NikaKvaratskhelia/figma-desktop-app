// Gemini Studio Figma Bridge Worker
figma.showUI(__html__, { width: 320, height: 40, themeColors: true });

function sendDocState() {
  figma.ui.postMessage({
    type: 'INITIAL_STATE',
    fileContext: {
      fileName: figma.root.name,
      pageName: figma.currentPage.name
    }
  });
}

// Initial broadcast
sendDocState();

figma.on('currentpagechange', sendDocState);

figma.ui.onmessage = async (msg) => {
  if (msg.type === 'REQUEST_INIT_STATE') {
    sendDocState();
    return;
  }

  if (msg.type === 'EXECUTE_CODE') {
    try {
      let codeToRun = (msg.code || '').trim();

      // If code ends with an un-awaited function call like `createButton();`, prepend `await `
      codeToRun = codeToRun.replace(/(^|[\n;])\s*([a-zA-Z0-9_$]+)\s*\(\s*\)\s*;?\s*$/, '$1await $2();');

      // If code is an un-awaited self-executing function `(async () => { ... })();`, prepend `return await `
      if (/^\s*\(?\s*async\s*\(/.test(codeToRun) && !codeToRun.startsWith('await ') && !codeToRun.startsWith('return ')) {
        codeToRun = 'return await ' + codeToRun;
      }

      // Evaluate within an async context
      const asyncFn = new Function('figma', `return (async () => {\n${codeToRun}\n})();`);
      const result = await asyncFn(figma);

      figma.ui.postMessage({
        id: msg.id,
        result: result !== undefined ? result : { success: true }
      });
    } catch (err) {
      figma.ui.postMessage({
        id: msg.id,
        error: err && err.message ? err.message : String(err)
      });
    }
  }
};
