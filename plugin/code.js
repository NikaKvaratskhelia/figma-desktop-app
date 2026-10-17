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
      // Evaluate within an async context
      const asyncFn = new Function('figma', `return (async () => { ${msg.code} })();`);
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
