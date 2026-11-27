//// ****************************************************************
// * SC Form Helpers
// * Small DOM builders shared by the telemetry and config pages
//// ****************************************************************

(function () {
  const root = globalThis;
  const SCPI = root.SCPI = root.SCPI || {};
  const ui = SCPI.ui = SCPI.ui || {};

  function el(tag, props = {}, children = []) {
    const node = document.createElement(tag);
    for (const [name, value] of Object.entries(props)) {
      if (name === 'class') {
        node.className = value;
      } else if (name === 'text') {
        node.textContent = value;
      } else if (name.startsWith('on')) {
        node.addEventListener(name.slice(2).toLowerCase(), value);
      } else if (value !== undefined && value !== null) {
        node.setAttribute(name, value);
      }
    }
    for (const child of [].concat(children)) {
      if (child) {
        node.appendChild(child);
      }
    }
    return node;
  }

  function row(label, content, title) {
    return el('div', {class: 'pi-picker-row'}, [
      el('div', {class: 'pi-picker-label', text: label, title}),
      content
    ]);
  }

  function textInput(value, placeholder, onInput, type = 'text') {
    const input = el('input', {type, placeholder: placeholder || '', 'aria-label': placeholder || ''});
    input.value = value ?? '';
    input.addEventListener('input', () => onInput(input.value));
    return el('div', {class: 'pi-dropdown'}, [
      el('div', {class: 'pi-dropdown__input-row'}, [
        el('div', {class: 'pi-dropdown__input-wrapper'}, [input])
      ])
    ]);
  }

  function selectInput(options, value, onChange, cls = 'pi-rule__select') {
    const select = el('select', {class: cls});
    for (const [optValue, optText] of options) {
      select.appendChild(el('option', {value: optValue, text: optText}));
    }
    select.value = options.some(([v]) => v === value) ? value : options[0]?.[0] ?? '';
    select.addEventListener('change', () => onChange(select.value));
    return select;
  }

  function flashTargetSelect(target, save) {
    return selectInput([
      ['text', 'Text'],
      ['background', 'Background + image'],
      ['everything', 'Everything']
    ], target.flashTarget || 'text', (v) => {
      target.flashTarget = v;
      save();
    }, 'pi-rule__select');
  }

  // Text and background color pickers editing `target.textColor` / `target.backgroundColor`.
  function colors(target, onChange) {
    function colorControl(key, caption) {
      const input = el('input', {type: 'color', class: 'pi-rule__color', title: caption});
      input.value = target[key] || (key === 'textColor' ? '#ffffff' : '#000000');
      const clear = el('button', {
        class: 'file-picker-clear', type: 'button', title: `Reset ${caption.toLowerCase()}`, text: 'X',
        onClick: () => {
          target[key] = null;
          input.value = key === 'textColor' ? '#ffffff' : '#000000';
          status.textContent = 'auto';
          onChange();
        }
      });
      const status = el('span', {class: 'pi-rule__hint', text: target[key] ? '' : 'auto'});
      input.addEventListener('input', () => {
        target[key] = input.value;
        status.textContent = '';
        onChange();
      });
      return el('div', {class: 'pi-rule__color-group'}, [
        el('span', {class: 'pi-rule__hint', text: caption}), input, clear, status
      ]);
    }

    return el('div', {class: 'pi-rule__colors'}, [
      colorControl('textColor', 'Text'),
      colorControl('backgroundColor', 'Back')
    ]);
  }

  ui.form = {el, row, textInput, selectInput, flashTargetSelect, colors};
})();
