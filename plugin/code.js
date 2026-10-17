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

      // If code ends with an un-awaited function call like `createButton();`, prepend `await `
      codeToRun = codeToRun.replace(/(^|[\n;])\s*([a-zA-Z0-9_$]+)\s*\(\s*\)\s*;?\s*$/, '$1await $2();');

      // If code is an un-awaited self-executing function `(async () => { ... })();`, prepend `return await `
      if (/^\s*\(?\s*async\s*\(/.test(codeToRun) && !codeToRun.startsWith('await ') && !codeToRun.startsWith('return ')) {
        codeToRun = 'return await ' + codeToRun;
      }

      // Safe font loader wrapper - does not mutate read-only global figma
      const originalLoadFont = figma.loadFontAsync.bind(figma);
      const safeLoadFontAsync = async (font) => {
        if (!font) return;
        try {
          return await originalLoadFont(font);
        } catch (err) {
          console.warn('[Gemini Bridge] Warning: Font failed to load, trying fallback:', font, err);
          // Try common variations so font errors never halt generation
          try {
            if (font.style === 'SemiBold' || font.style === 'semibold') {
              return await originalLoadFont({ family: font.family, style: 'Semi Bold' });
            }
          } catch (_) {}
          try {
            return await originalLoadFont({ family: font.family || 'Inter', style: 'Bold' });
          } catch (_) {}
          try {
            return await originalLoadFont({ family: 'Inter', style: 'Regular' });
          } catch (_) {
            // Silently ignore so execution proceeds
            return;
          }
        }
      };

      // Wrap figma in a proxy to safely intercept loadFontAsync without mutating read-only properties
      const figmaProxy = new Proxy(figma, {
        get(target, prop) {
          if (prop === 'loadFontAsync') {
            return safeLoadFontAsync;
          }
          const val = target[prop];
          return typeof val === 'function' ? val.bind(target) : val;
        }
      });

      // Pre-load standard Inter baseline fonts
      try { await originalLoadFont({ family: 'Inter', style: 'Regular' }); } catch (_) {}
      try { await originalLoadFont({ family: 'Inter', style: 'Bold' }); } catch (_) {}

      // Evaluate within an async context passing figmaProxy as local figma parameter
      const asyncFn = new Function('figma', `return (async () => {\n${codeToRun}\n})();`);
      const result = await asyncFn(figmaProxy);

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
