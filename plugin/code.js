// Gemini Studio Figma Bridge Worker
figma.showUI(__html__, { width: 320, height: 40, themeColors: true });

function sendDocState() {
  figma.ui.postMessage({
    type: 'INITIAL_STATE',
    fileContext: {
      fileName: figma.root ? figma.root.name : 'Untitled',
      pageName: figma.currentPage ? figma.currentPage.name : 'Page 1'
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

      // Normalize font styles that are prone to name mismatch in Figma
      codeToRun = codeToRun.replace(/(['"])SemiBold\1/gi, "$1Bold$1");
      codeToRun = codeToRun.replace(/(['"])Semi-Bold\1/gi, "$1Bold$1");
      codeToRun = codeToRun.replace(/(['"])ExtraBold\1/gi, "$1Bold$1");

      // Replace figma.loadFontAsync with safe internal helper so font errors never halt execution
      codeToRun = codeToRun.replace(/figma\.loadFontAsync/g, 'safeLoadFontAsync');

      // If code ends with an un-awaited function call like `createButton();`, prepend `await `
      codeToRun = codeToRun.replace(/(^|[\n;])\s*([a-zA-Z0-9_$]+)\s*\(\s*\)\s*;?\s*$/, '$1await $2();');

      // If code is an un-awaited self-executing function `(async () => { ... })();`, prepend `return await `
      if (/^\s*\(?\s*async\s*\(/.test(codeToRun) && !codeToRun.startsWith('await ') && !codeToRun.startsWith('return ')) {
        codeToRun = 'return await ' + codeToRun;
      }

      // Pre-load standard baseline fonts
      try { await figma.loadFontAsync({ family: 'Inter', style: 'Regular' }); } catch (_) {}
      try { await figma.loadFontAsync({ family: 'Inter', style: 'Bold' }); } catch (_) {}

      // Evaluated function with safeLoadFontAsync helper and native figma object (no Proxy)
      const wrapperScript = `
        const safeLoadFontAsync = async (font) => {
          if (!font) return;
          try {
            return await figma.loadFontAsync(font);
          } catch (err) {
            console.warn('[Gemini Bridge] Warning: Font failed to load, falling back:', font);
            try {
              if (font.style === 'SemiBold' || font.style === 'semibold') {
                return await figma.loadFontAsync({ family: font.family, style: 'Semi Bold' });
              }
            } catch (_) {}
            try {
              return await figma.loadFontAsync({ family: font.family || 'Inter', style: 'Bold' });
            } catch (_) {}
            try {
              return await figma.loadFontAsync({ family: 'Inter', style: 'Regular' });
            } catch (_) {
              return;
            }
          }
        };

        return (async () => {
          ${codeToRun}
        })();
      `;

      const asyncFn = new Function('figma', wrapperScript);
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
