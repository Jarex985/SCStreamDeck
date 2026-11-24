//// ****************************************************************
// * Telemetry Display PI Entrypoint
// * Attribute picker + ordered rules (value -> text / image / colors / press action)
//// ****************************************************************

(function () {
  const SCPI = globalThis.SCPI;
  SCPI?.bus?.start?.();

  // Advanced page: rules hold conditions on several attributes (set by AdvancedTelemetryDisplay.html).
  const ADVANCED = globalThis.SCPI_TELEMETRY_ADVANCED === true;

  // Deceleration page: numeric attributes only, plus the estimate options (set by Deceleration.html).
  const DECEL = globalThis.SCPI_TELEMETRY_DECELERATION === true;

  // #region State

  const OPS_NUMBER = [
    ['lt', 'is less than'],
    ['lte', 'is at most'],
    ['gt', 'is greater than'],
    ['gte', 'is at least'],
    ['between', 'is between'],
    ['eq', 'equals'],
    ['neq', 'does not equal']
  ];
  const OPS_EQUALITY = [
    ['eq', 'is'],
    ['neq', 'is not']
  ];

  const state = {
    fieldPath: '',
    label: '',
    decimals: null,
    rules: [],
    defaultRule: {op: 'always'}
  };

  let fields = [];
  let functionOptions = [];
  let functionsLoaded = null;

  const setters = {};

  // #endregion

  // #region Settings

  function useSetting(key, onLoaded) {
    const [get, set] = globalThis.SDPIComponents.useSettings(key);
    setters[key] = set;
    Promise.resolve(get()).then((value) => onLoaded(value)).catch(() => {
    });
  }

  function save(key, value) {
    // Always hand over a copy: the SDPI store ignores writes of the same object reference.
    const copy = value === undefined || value === null ? null : JSON.parse(JSON.stringify(value));
    setters[key]?.(copy);
  }

  function saveRules() {
    save('rules', state.rules);
    save('defaultRule', state.defaultRule);
  }

  // #endregion

  // #region Helpers

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

  function currentField(path = state.fieldPath) {
    return fields.find((f) => f.path === path) || null;
  }

  function fieldKind(path = state.fieldPath) {
    return String(currentField(path)?.kind || '').toLowerCase();
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

  // #endregion

  // #region Attribute section

  const fieldDropdown = SCPI?.ui?.dropdown?.initDropdown?.({
    rootId: 'fieldDropdown',
    searchEnabled: true,
    minLoadingMs: 0,
    successFlashMs: 100,
    emptyText: 'No matching attributes',
    maxResults: 80,
    getText: (opt) => String(opt?.text ?? ''),
    getValue: (opt) => String(opt?.value ?? ''),
    onSelect: (opt) => selectField(opt.value)
  });
  fieldDropdown?.setLoading?.(true, 'Loading attributes');

  function populateFields(list) {
    fields = Array.isArray(list) ? list : [];
    if (DECEL) {
      fields = fields.filter((f) => String(f.kind).toLowerCase() === 'number');
    }
    fieldDropdown?.setItems?.(fields.map((f) => ({
      value: f.path,
      text: `${f.group} › ${f.label}`
    })));
    fieldDropdown?.setLoading?.(false);
    refreshFieldSummary();
    renderRules();
  }

  function selectField(path) {
    state.fieldPath = path;
    save('fieldPath', path);
    refreshFieldSummary();
    // Operators and value editors depend on the attribute type (simple rules only).
    if (!ADVANCED) {
      state.rules.forEach((rule) => normalizeForField(rule, currentField()));
    }
    saveRules();
    renderRules();
  }

  document.getElementById('fieldClear')?.addEventListener('click', () => selectField(''));

  function refreshFieldSummary() {
    const field = currentField();
    const target = document.getElementById('fieldCurrent');
    if (target) {
      target.textContent = field
        ? `${field.group} › ${field.label}  (${field.kind})`
        : (state.fieldPath ? `${state.fieldPath} (unknown attribute)` : 'No attribute selected');
    }
    const labelInput = document.getElementById('labelInput');
    if (labelInput) {
      labelInput.placeholder = field ? field.label : 'Attribute name';
    }
    const clear = document.getElementById('fieldClear');
    if (clear) {
      clear.disabled = !state.fieldPath;
    }
  }

  // Makes op/value valid for the attribute's type; works for rules and for conditions.
  function normalizeForField(target, field) {
    const kind = String(field?.kind || '').toLowerCase();
    if (!kind) {
      return;
    }
    const allowed = (kind === 'number' ? OPS_NUMBER : OPS_EQUALITY).map(([v]) => v);
    if (!allowed.includes(target.op)) {
      target.op = allowed[0];
      target.value = '';
      target.value2 = '';
    }
    if (kind === 'boolean' && target.value !== 'true' && target.value !== 'false') {
      target.value = 'true';
    }
    if (kind === 'enum') {
      const names = (field?.enumValues || []).map((e) => e.name);
      if (!names.includes(target.value)) {
        target.value = names[0] || '';
      }
    }
  }

  const labelInput = document.getElementById('labelInput');
  labelInput?.addEventListener('input', () => {
    state.label = labelInput.value;
    save('label', state.label);
  });

  const decimalsInput = document.getElementById('decimalsInput');
  decimalsInput?.addEventListener('input', () => {
    const parsed = parseInt(decimalsInput.value, 10);
    state.decimals = Number.isFinite(parsed) ? Math.min(4, Math.max(0, parsed)) : null;
    save('decimals', state.decimals);
  });

  // Deceleration options: window, output, rounding steps and unit suffix.
  const decelInputs = {
    windowMs: document.getElementById('decelWindow'),
    output: document.getElementById('decelOutput'),
    unitsRounding: document.getElementById('decelUnitsRounding'),
    timeRounding: document.getElementById('decelTimeRounding'),
    unit: document.getElementById('decelUnit')
  };

  function readNumber(input, integer) {
    const parsed = integer ? parseInt(input.value, 10) : parseFloat(input.value);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
  }

  function saveDeceleration() {
    state.deceleration = {
      windowMs: readNumber(decelInputs.windowMs, true),
      output: decelInputs.output?.value || 'both',
      unitsRounding: readNumber(decelInputs.unitsRounding, false),
      timeRounding: readNumber(decelInputs.timeRounding, false),
      unit: decelInputs.unit?.value?.trim() || null
    };
    save('deceleration', state.deceleration);
  }

  function showDeceleration(value) {
    const d = value && typeof value === 'object' ? value : {};
    state.deceleration = d;
    if (decelInputs.windowMs) decelInputs.windowMs.value = d.windowMs ?? '';
    if (decelInputs.output) decelInputs.output.value = d.output || 'both';
    if (decelInputs.unitsRounding) decelInputs.unitsRounding.value = d.unitsRounding ?? '';
    if (decelInputs.timeRounding) decelInputs.timeRounding.value = d.timeRounding ?? '';
    if (decelInputs.unit) decelInputs.unit.value = d.unit ?? '';
  }

  Object.values(decelInputs).forEach((input) => {
    input?.addEventListener('input', saveDeceleration);
    input?.addEventListener('change', saveDeceleration);
  });

  // #endregion

  // #region Rules

  function newCondition() {
    return {fieldPath: '', op: 'eq', value: '', value2: ''};
  }

  function newRule() {
    if (ADVANCED) {
      return {match: 'all', conditions: [newCondition()]};
    }
    const rule = {op: fieldKind() === 'number' ? 'lt' : 'eq', value: '', value2: ''};
    normalizeForField(rule, currentField());
    return rule;
  }

  function renderRules() {
    const list = document.getElementById('rulesList');
    const defaults = document.getElementById('defaultRule');
    if (!list || !defaults) {
      return;
    }

    list.replaceChildren(...state.rules.map((rule, index) => renderRuleCard(rule, index, false)));
    defaults.replaceChildren(renderRuleCard(state.defaultRule, -1, true));
  }

  function renderRuleCard(rule, index, isDefault) {
    const idPrefix = isDefault ? 'rule-default' : `rule-${index}`;
    const title = isDefault ? 'Default (no rule matches / no data)' : `Rule ${index + 1}`;

    const header = el('div', {class: 'pi-rule__header'}, [el('span', {class: 'pi-rule__title', text: title})]);
    if (!isDefault) {
      const actions = el('div', {class: 'pi-rule__actions'}, [
        el('button', {
          class: 'file-picker-clear', type: 'button', title: 'Move up', text: '▲', onClick: () => moveRule(index, -1)
        }),
        el('button', {
          class: 'file-picker-clear', type: 'button', title: 'Move down', text: '▼', onClick: () => moveRule(index, 1)
        }),
        el('button', {
          class: 'file-picker-clear', type: 'button', title: 'Remove rule', text: 'X', onClick: () => removeRule(index)
        })
      ]);
      header.appendChild(actions);
    }

    const card = el('div', {class: 'pi-rule'}, [header]);

    if (!isDefault) {
      card.appendChild(row('When', ADVANCED ? renderConditions(rule) : renderCondition(rule)));
    }

    card.appendChild(row('Text', textInput(rule.text, '{label}\\n{value}', (v) => {
      rule.text = v;
      saveRules();
    }), ADVANCED
      ? 'Use {value}/{label} (display attribute) or {value:attribute.path} for any attribute; \\n starts a new line.'
      : DECEL
        ? 'Use {units}, {time}, {value} (the selected output) and {label}; \\n starts a new line. Empty = the default for the chosen output.'
        : 'Use {value} and {label}; \\n starts a new line. Empty = "{label}\\n{value}".'));

    card.appendChild(row('Image', el('div', {
      class: 'pi-file-picker', id: `${idPrefix}-image`, 'data-accept': '.png,.jpg,.jpeg,.bmp,.gif',
      'data-placeholder': 'No image', 'data-select-title': 'Select image', 'data-clear-title': 'Clear image'
    })));

    if (!isDefault) {
      card.appendChild(row('Sound', el('div', {
        class: 'pi-file-picker', id: `${idPrefix}-sound`, 'data-accept': '.wav,.mp3',
        'data-placeholder': 'No sound', 'data-select-title': 'Select audio file', 'data-clear-title': 'Clear audio file'
      }), 'Played once when this rule becomes active.'));
    }

    card.appendChild(row('Colors', renderColors(rule)));
    card.appendChild(row('Flash', textInput(rule.flashMs, 'off (ms)', (v) => {
      const parsed = parseInt(v, 10);
      rule.flashMs = Number.isFinite(parsed) && parsed > 0 ? parsed : null;
      saveRules();
    }, 'number'), 'Show the text/image for this many ms, then hide it for the same time, repeating. Minimum 250. Empty = no flashing.'));
    card.appendChild(row('Press', renderPressAction(rule, idPrefix),
      ADVANCED ? 'Actions run, in order, when the key is pressed while this rule is active.'
        : 'Action run when the key is pressed while this rule is active.'));

    queueMicrotask(() => {
      if (!isDefault) {
        SCPI?.ui?.filePicker?.createFilePicker?.({
          rootId: `${idPrefix}-sound`,
          placeholderText: 'No sound',
          initialValue: rule.soundPath || '',
          onValueChanged: (path) => {
            rule.soundPath = path || null;
            saveRules();
          }
        });
      }

      SCPI?.ui?.filePicker?.createFilePicker?.({
        rootId: `${idPrefix}-image`,
        placeholderText: 'No image',
        initialValue: rule.imagePath || '',
        onValueChanged: (path) => {
          rule.imagePath = path || null;
          saveRules();
        }
      });
    });

    return card;
  }

  function renderCondition(rule) {
    return renderOperatorEditor(rule, currentField());
  }

  // Advanced rules: all/any selector plus one row per condition (attribute + operator + value).
  function renderConditions(rule) {
    rule.conditions = Array.isArray(rule.conditions) ? rule.conditions : [];
    rule.match = rule.match === 'any' ? 'any' : 'all';

    const wrap = el('div', {class: 'pi-rule__conditions'});
    wrap.appendChild(selectInput([['all', 'All of these (AND)'], ['any', 'Any of these (OR)']], rule.match, (m) => {
      rule.match = m;
      saveRules();
    }));

    rule.conditions.forEach((condition, index) => {
      const field = currentField(condition.fieldPath);
      const line = el('div', {class: 'pi-rule__condition-row'});

      line.appendChild(attributeSelect(condition.fieldPath, (path) => {
        condition.fieldPath = path;
        condition.value = '';
        condition.value2 = '';
        normalizeForField(condition, currentField(path));
        saveRules();
        renderRules();
      }));
      line.appendChild(renderOperatorEditor(condition, field));
      line.appendChild(el('button', {
        class: 'file-picker-clear', type: 'button', title: 'Remove condition', text: 'X',
        onClick: () => {
          rule.conditions.splice(index, 1);
          saveRules();
          renderRules();
        }
      }));
      wrap.appendChild(line);
    });

    wrap.appendChild(el('button', {
      class: 'pi-button', type: 'button', text: 'Add condition',
      onClick: () => {
        rule.conditions.push(newCondition());
        saveRules();
        renderRules();
      }
    }));

    return wrap;
  }

  function attributeSelect(path, onChange) {
    const select = el('select', {class: 'pi-rule__select pi-rule__attribute'});
    select.appendChild(el('option', {value: '', text: 'Select attribute...'}));

    const groups = new Map();
    for (const f of fields) {
      if (!groups.has(f.group)) {
        groups.set(f.group, el('optgroup', {label: f.group}));
        select.appendChild(groups.get(f.group));
      }
      groups.get(f.group).appendChild(el('option', {value: f.path, text: f.label}));
    }

    select.value = fields.some((f) => f.path === path) ? path : '';
    select.addEventListener('change', () => onChange(select.value));
    return select;
  }

  // Operator + value editors for a rule (simple key) or a condition (advanced key) on the given attribute.
  function renderOperatorEditor(rule, field) {
    const kind = String(field?.kind || '').toLowerCase();
    const wrap = el('div', {class: 'pi-rule__condition'});

    if (!kind) {
      wrap.appendChild(el('span', {class: 'pi-rule__hint', text: 'Select an attribute first'}));
      return wrap;
    }

    const ops = kind === 'number' ? OPS_NUMBER : OPS_EQUALITY;
    wrap.appendChild(selectInput(ops, rule.op, (op) => {
      rule.op = op;
      saveRules();
      renderRules();
    }));

    if (kind === 'boolean') {
      wrap.appendChild(selectInput([['true', 'On'], ['false', 'Off']], rule.value, (v) => {
        rule.value = v;
        saveRules();
      }));
    } else if (kind === 'enum') {
      const options = (field?.enumValues || []).map((e) => [e.name, e.label]);
      wrap.appendChild(selectInput(options, rule.value, (v) => {
        rule.value = v;
        saveRules();
      }));
    } else {
      const numeric = kind === 'number';
      wrap.appendChild(el('div', {class: 'pi-rule__value'}, [
        textInput(rule.value, 'value', (v) => {
          rule.value = v;
          saveRules();
        }, numeric ? 'number' : 'text')
      ]));
      if (numeric && rule.op === 'between') {
        wrap.appendChild(el('span', {class: 'pi-rule__hint', text: 'and'}));
        wrap.appendChild(el('div', {class: 'pi-rule__value'}, [
          textInput(rule.value2, 'value', (v) => {
            rule.value2 = v;
            saveRules();
          }, 'number')
        ]));
      }
    }

    return wrap;
  }

  function renderColors(rule) {
    function colorControl(key, caption) {
      const input = el('input', {type: 'color', class: 'pi-rule__color', title: caption});
      input.value = rule[key] || (key === 'textColor' ? '#ffffff' : '#000000');
      const clear = el('button', {
        class: 'file-picker-clear', type: 'button', title: `Reset ${caption.toLowerCase()}`, text: 'X',
        onClick: () => {
          rule[key] = null;
          input.value = key === 'textColor' ? '#ffffff' : '#000000';
          status.textContent = 'auto';
          saveRules();
        }
      });
      const status = el('span', {class: 'pi-rule__hint', text: rule[key] ? '' : 'auto'});
      input.addEventListener('input', () => {
        rule[key] = input.value;
        status.textContent = '';
        saveRules();
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

  function renderPressAction(rule, idPrefix) {
    return ADVANCED
      ? renderPressActions(rule, idPrefix)
      : renderFunctionPicker(rule, `${idPrefix}-function`, () => saveRules());
  }

  // Advanced rules: an ordered list of actions, each with an optional delay before it.
  function renderPressActions(rule, idPrefix) {
    // A single legacy `function` becomes the first entry of the list.
    if (!Array.isArray(rule.actions)) {
      rule.actions = rule.function ? [{function: rule.function, delayMs: null}] : [];
    }
    rule.function = null;

    const wrap = el('div', {class: 'pi-rule__actions-list'});

    rule.actions.forEach((action, index) => {
      const line = el('div', {class: 'pi-rule__action'});

      const header = el('div', {class: 'pi-rule__action-header'}, [
        el('span', {class: 'pi-rule__hint', text: `${index + 1}.`}),
        el('div', {class: 'pi-rule__value'}, [
          textInput(action.delayMs, 'delay (ms)', (v) => {
            const parsed = parseInt(v, 10);
            action.delayMs = Number.isFinite(parsed) && parsed > 0 ? parsed : null;
            saveRules();
          }, 'number')
        ]),
        el('button', {
          class: 'file-picker-clear', type: 'button', title: 'Move up', text: '▲', onClick: () => moveAction(rule, index, -1)
        }),
        el('button', {
          class: 'file-picker-clear', type: 'button', title: 'Move down', text: '▼', onClick: () => moveAction(rule, index, 1)
        }),
        el('button', {
          class: 'file-picker-clear', type: 'button', title: 'Remove action', text: 'X',
          onClick: () => {
            rule.actions.splice(index, 1);
            saveRules();
            renderRules();
          }
        })
      ]);

      line.appendChild(header);
      line.appendChild(renderFunctionPicker(action, `${idPrefix}-action-${index}`, () => saveRules(), false));
      wrap.appendChild(line);
    });

    wrap.appendChild(el('button', {
      class: 'pi-button', type: 'button', text: 'Add action',
      onClick: () => {
        rule.actions.push({function: null, delayMs: null});
        saveRules();
        renderRules();
      }
    }));

    wrap.appendChild(el('span', {
      class: 'pi-rule__hint',
      text: 'Actions run in order when the key is pressed (a single action without delay can be held).'
    }));

    return wrap;
  }

  function moveAction(rule, index, delta) {
    const target = index + delta;
    if (target < 0 || target >= rule.actions.length) {
      return;
    }
    [rule.actions[index], rule.actions[target]] = [rule.actions[target], rule.actions[index]];
    saveRules();
    renderRules();
  }

  // Shows the function stored in `target.function` with a clear button and a searchable picker.
  function renderFunctionPicker(target, rootId, onChanged, showClear = true) {
    const nameFor = (value) => functionOptions.find((o) => o.value === value)?.text || value || '';

    const current = el('span', {
      class: 'pi-rule__function', text: target.function ? nameFor(target.function) : 'None'
    });
    const currentRow = el('div', {class: 'pi-rule__press-current'}, [current]);
    if (showClear) {
      currentRow.appendChild(el('button', {
        class: 'file-picker-clear', type: 'button', title: 'Clear press action', text: 'X',
        onClick: () => {
          target.function = null;
          current.textContent = 'None';
          onChanged();
        }
      }));
    }

    const dropdownRoot = el('div', {
      class: 'pi-dropdown', id: rootId, 'data-placeholder': 'Search functions...'
    });

    queueMicrotask(() => {
      const dropdown = SCPI?.ui?.dropdown?.initDropdown?.({
        rootId,
        searchEnabled: true,
        minLoadingMs: 0,
        successFlashMs: 100,
        emptyText: 'No matching functions found',
        maxResults: 50,
        getText: (opt) => String(opt?.text ?? ''),
        getValue: (opt) => String(opt?.value ?? ''),
        getGroup: (opt) => String(opt?.group ?? ''),
        isDisabled: (opt) => !!opt?.disabled,
        onSelect: (opt) => {
          target.function = opt.value;
          current.textContent = opt.text;
          onChanged();
        }
      });

      if (functionsLoaded === false) {
        dropdown?.setLoading?.(true, 'No installation detected. Set custom path.');
      } else if (functionsLoaded === null) {
        dropdown?.setLoading?.(true, 'Loading functions');
      } else {
        dropdown?.setItems?.(functionOptions);
      }
    });

    return el('div', {class: 'pi-rule__press'}, [currentRow, dropdownRoot]);
  }

  function moveRule(index, delta) {
    const target = index + delta;
    if (target < 0 || target >= state.rules.length) {
      return;
    }
    [state.rules[index], state.rules[target]] = [state.rules[target], state.rules[index]];
    saveRules();
    renderRules();
  }

  function removeRule(index) {
    state.rules.splice(index, 1);
    saveRules();
    renderRules();
  }

  document.getElementById('addRule')?.addEventListener('click', () => {
    state.rules.push(newRule());
    saveRules();
    renderRules();
  });

  // #endregion

  // #region Functions payload (same message the other keys receive)

  function flattenFunctions(groups) {
    const flat = [];
    for (const group of Array.isArray(groups) ? groups : []) {
      const groupName = group.label || 'Other';
      for (const opt of group.options || []) {
        const bindingType = String(opt.bindingType || '').toLowerCase();
        if (bindingType === 'mouseaxis' || bindingType === 'joystick' || bindingType === 'gamepad') {
          continue;
        }
        const unbound = bindingType === 'unbound';
        flat.push({
          value: opt.value,
          text: opt.text,
          group: groupName,
          unbound,
          disabled: !!opt.disabled && !unbound
        });
      }
    }
    return flat;
  }

  SCPI?.bus?.on?.((payload) => {
    if (Array.isArray(payload?.telemetryFields)) {
      populateFields(payload.telemetryFields);
    }

    if (payload?.functionsLoaded === true) {
      functionsLoaded = true;
      functionOptions = flattenFunctions(payload.functions);
      renderRules();
    } else if (payload?.functionsLoaded === false) {
      functionsLoaded = false;
      functionOptions = [];
      renderRules();
    }
  });

  // #endregion

  // #region Startup

  useSetting('fieldPath', (v) => {
    state.fieldPath = typeof v === 'string' ? v : '';
    refreshFieldSummary();
    renderRules();
  });
  useSetting('label', (v) => {
    state.label = typeof v === 'string' ? v : '';
    if (labelInput) {
      labelInput.value = state.label;
    }
  });
  useSetting('deceleration', (v) => showDeceleration(v));
  useSetting('decimals', (v) => {
    state.decimals = Number.isFinite(v) ? v : null;
    if (decimalsInput) {
      decimalsInput.value = state.decimals === null ? '' : String(state.decimals);
    }
  });
  useSetting('rules', (v) => {
    state.rules = Array.isArray(v) ? v : [];
    renderRules();
  });
  useSetting('defaultRule', (v) => {
    state.defaultRule = v && typeof v === 'object' ? v : {op: 'always'};
    state.defaultRule.op = 'always';
    renderRules();
  });

  SCPI?.ui?.filePicker?.createFilePicker?.({
    rootId: 'audioFilePicker',
    placeholderText: 'No file selected',
    settingsKey: 'clickSoundPath'
  });

  SCPI?.util?.onDocumentReady?.(() => {
    renderRules();
    SCPI?.bus?.sendOnce?.('pi.connected', 'propertyInspectorConnected');
  });

  // #endregion
})();
