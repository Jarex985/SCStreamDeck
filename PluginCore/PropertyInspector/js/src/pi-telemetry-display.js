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

  // UEC tracker page: no attribute picker; rules can test the balance and the session change (set by UecTracker.html).
  const UEC = globalThis.SCPI_TELEMETRY_UEC === true;

  // Travel pages: no attribute picker; rules can test the measurement. 'timer' (start/stop key) or 'display'.
  const TRAVEL = globalThis.SCPI_TELEMETRY_TRAVEL || null;

  // Travel Timer page: Start and Stop looks plus rules that make the key unavailable instead of a default look.
  const TIMER = TRAVEL === 'timer';

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
    templates: [],
    defaultRule: {op: 'always'},
    startRule: {op: 'always'},
    stopRule: {op: 'always'}
  };

  let fields = [];

  // Pseudo attributes of the deceleration estimate that rules can test (resolved by the plugin).
  const DECEL_FIELDS = [
    {path: 'deceleration.units', label: 'Estimate (units)', group: 'Deceleration', kind: 'Number', isInteger: false, enumValues: null},
    {path: 'deceleration.time', label: 'Estimate (time, s)', group: 'Deceleration', kind: 'Number', isInteger: false, enumValues: null}
  ];

  // Pseudo attribute of the UEC tracker: gain (positive) or loss (negative) since the session started.
  const UEC_FIELDS = [
    {path: 'uec.change', label: 'Session change', group: 'UEC', kind: 'Number', isInteger: true, enumValues: null}
  ];

  // Pseudo attributes of the travel measurement (resolved by the plugin).
  const TRAVEL_FIELDS = [
    {
      path: 'travel.state', label: 'State', group: 'Travel', kind: 'Enum', isInteger: false,
      enumValues: [{name: 'IDLE', label: 'Idle'}, {name: 'RUNNING', label: 'Running'}, {name: 'STOPPED', label: 'Stopped'}]
    },
    {path: 'travel.minutes', label: 'Elapsed (min)', group: 'Travel', kind: 'Number', isInteger: false, enumValues: null},
    {path: 'travel.meters', label: 'Distance (m)', group: 'Travel', kind: 'Number', isInteger: false, enumValues: null}
  ];
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
    if (TIMER) {
      save('startRule', state.startRule);
      save('stopRule', state.stopRule);
    }
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
    const real = Array.isArray(list) ? list : [];
    // Condition editors can test any attribute (and, on the deceleration page, the estimate itself);
    // the primary attribute of the deceleration key must be numeric.
    fields = DECEL ? real.concat(DECEL_FIELDS) : UEC ? real.concat(UEC_FIELDS) : TRAVEL ? real.concat(TRAVEL_FIELDS) : real;
    const primary = DECEL ? real.filter((f) => String(f.kind).toLowerCase() === 'number') : real;
    fieldDropdown?.setItems?.(primary.map((f) => ({
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
    unitsDivisor: document.getElementById('decelDivisor'),
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
      unitsDivisor: readNumber(decelInputs.unitsDivisor, false),
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
    if (decelInputs.unitsDivisor) decelInputs.unitsDivisor.value = d.unitsDivisor ?? '';
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
    if (TIMER) {
      const start = document.getElementById('startRule');
      const stop = document.getElementById('stopRule');
      if (!list || !start || !stop) {
        return;
      }

      list.replaceChildren(...state.rules.map((rule, index) => renderRuleCard(rule, index, false)));
      start.replaceChildren(renderRuleCard(state.startRule, -1, true, {
        id: 'start', title: 'Start (shown while a measurement can be started; pressing starts it)', placeholder: 'Start'
      }));
      stop.replaceChildren(renderRuleCard(state.stopRule, -1, true, {
        id: 'stop', title: 'Stop (shown while measuring; pressing stops it)', placeholder: 'Stop'
      }));
      return;
    }

    const defaults = document.getElementById('defaultRule');
    if (!list || !defaults) {
      return;
    }

    list.replaceChildren(...state.rules.map((rule, index) => renderRuleCard(rule, index, false)));
    defaults.replaceChildren(renderRuleCard(state.defaultRule, -1, true));
  }

  // `section` (Travel Timer Start/Stop looks) is a card without conditions that still has sound and press actions.
  function renderRuleCard(rule, index, isDefault, section = null) {
    const idPrefix = section ? `rule-${section.id}` : isDefault ? 'rule-default' : `rule-${index}`;
    const title = section ? section.title : isDefault ? 'Default (no rule matches / no data)'
      : TIMER ? `Unavailable when (rule ${index + 1})` : `Rule ${index + 1}`;
    const showSound = !isDefault || !!section;
    // Pressing an unavailable key does nothing, so its rules have no press actions.
    const showPress = !(TIMER && !section);
    const textPlaceholder = section?.placeholder || (TIMER ? 'Unavailable' : null);

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

    card.appendChild(row('Text', textInput(rule.text, textPlaceholder || (TRAVEL ? '{elapsed}\\n{distance}' : UEC ? '{value}\\n{change}' : '{label}\\n{value}'), (v) => {
      rule.text = v;
      saveRules();
    }), TIMER
      ? `Use {state}, {elapsed} (min), {distance}, {label} or {value:attribute.path}; \\n starts a new line. Empty = ${textPlaceholder}.`
      : TRAVEL
      ? 'Use {elapsed} (min), {distance} (m, then km), {state}, {label} or {value:attribute.path}; \\n starts a new line. Empty = "{elapsed}\\n{distance}".'
      : UEC
      ? 'Use {value} (balance), {change} (earned/lost), {label} or {value:attribute.path}; \\n starts a new line. Empty = "{value}\\n{change}".'
      : DECEL
      ? 'Use {units}, {time}, {value} (the selected output), {label} or {value:attribute.path}; \\n starts a new line. Empty = the default for the chosen output.'
      : ADVANCED
        ? 'Use {value}/{label} (display attribute) or {value:attribute.path} for any attribute; \\n starts a new line.'
        : 'Use {value} and {label}; \\n starts a new line. Empty = "{label}\\n{value}".'));

    card.appendChild(row('Image', el('div', {
      class: 'pi-file-picker', id: `${idPrefix}-image`, 'data-accept': '.png,.jpg,.jpeg,.bmp,.gif',
      'data-placeholder': 'No image', 'data-select-title': 'Select image', 'data-clear-title': 'Clear image'
    })));

    if (showSound) {
      card.appendChild(row('Sound', el('div', {
        class: 'pi-file-picker', id: `${idPrefix}-sound`, 'data-accept': '.wav,.mp3',
        'data-placeholder': 'No sound', 'data-select-title': 'Select audio file', 'data-clear-title': 'Clear audio file'
      }), 'Played once when this rule becomes active.'));
    }

    card.appendChild(row('Template', templateSelect(rule),
      'Colors, image and flash from a shared format template. Anything set on this rule itself overrides the template.'));
    card.appendChild(row('Colors', renderColors(rule)));
    card.appendChild(row('Flash', textInput(rule.flashMs, 'off (ms)', (v) => {
      const parsed = parseInt(v, 10);
      rule.flashMs = Number.isFinite(parsed) && parsed > 0 ? parsed : null;
      saveRules();
    }, 'number'), 'Show the content for this many ms, then hide it for the same time, repeating. Minimum 250. Empty = no flashing.'));
    card.appendChild(row('Flashes', flashTargetSelect(rule, saveRules), 'Which part blinks while flashing. Background and Everything switch to black while off.'));
    if (showPress) {
      card.appendChild(row('Press', renderPressAction(rule, idPrefix),
        ADVANCED ? 'Actions run, in order, when the key is pressed while this rule is active.'
          : 'Action run when the key is pressed while this rule is active.'));
    }

    queueMicrotask(() => {
      if (showSound) {
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
    // A rule saved before the deceleration key supported conditions tested the estimate with op/value.
    if (DECEL && !Array.isArray(rule.conditions) && rule.op && rule.op !== 'always') {
      rule.conditions = [{
        fieldPath: state.deceleration?.output === 'time' ? 'deceleration.time' : 'deceleration.units',
        op: rule.op, value: rule.value || '', value2: rule.value2 || ''
      }];
      rule.match = 'all';
      rule.op = 'always';
      rule.value = '';
      rule.value2 = '';
    }

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

  function renderColors(rule, onChange = saveRules) {
    function colorControl(key, caption) {
      const input = el('input', {type: 'color', class: 'pi-rule__color', title: caption});
      input.value = rule[key] || (key === 'textColor' ? '#ffffff' : '#000000');
      const clear = el('button', {
        class: 'file-picker-clear', type: 'button', title: `Reset ${caption.toLowerCase()}`, text: 'X',
        onClick: () => {
          rule[key] = null;
          input.value = key === 'textColor' ? '#ffffff' : '#000000';
          status.textContent = 'auto';
          onChange();
        }
      });
      const status = el('span', {class: 'pi-rule__hint', text: rule[key] ? '' : 'auto'});
      input.addEventListener('input', () => {
        rule[key] = input.value;
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

  // #region Format templates (shared by all telemetry keys through the plugin's global settings)

  let selectedTemplateId = '';

  function saveTemplates() {
    setTemplates?.(JSON.parse(JSON.stringify(state.templates)));
  }

  function newTemplateId() {
    return globalThis.crypto?.randomUUID?.() ?? `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
  }

  function templateName(template) {
    return String(template?.name || '').trim() || '(unnamed)';
  }

  function templateSelect(rule) {
    const options = [['', 'None'], ...state.templates.map((t) => [t.id, templateName(t)])];
    return selectInput(options, rule.templateId || '', (id) => {
      rule.templateId = id || null;
      saveRules();
    }, 'pi-rule__select pi-rule__template');
  }

  function renderTemplates() {
    const host = document.getElementById('templates');
    if (!host) {
      return;
    }

    if (!state.templates.some((t) => t.id === selectedTemplateId)) {
      selectedTemplateId = state.templates[0]?.id || '';
    }
    const selected = state.templates.find((t) => t.id === selectedTemplateId);

    const toolbar = el('div', {class: 'pi-rule__condition'}, []);
    if (state.templates.length > 0) {
      toolbar.appendChild(selectInput(
        state.templates.map((t) => [t.id, templateName(t)]), selectedTemplateId, (id) => {
          selectedTemplateId = id;
          renderTemplates();
        }, 'pi-rule__select pi-rule__template'));
    }
    toolbar.appendChild(el('button', {
      class: 'pi-button', type: 'button', text: 'New template',
      onClick: () => {
        const template = {id: newTemplateId(), name: `Template ${state.templates.length + 1}`};
        state.templates.push(template);
        selectedTemplateId = template.id;
        saveTemplates();
        renderTemplates();
        renderRules();
      }
    }));

    const children = [toolbar];

    if (selected) {
      toolbar.appendChild(el('button', {
        class: 'file-picker-clear', type: 'button', title: 'Delete this template', text: 'X',
        onClick: () => {
          state.templates = state.templates.filter((t) => t.id !== selected.id);
          saveTemplates();
          renderTemplates();
          renderRules();
        }
      }));

      children.push(row('Name', textInput(selected.name, 'Template name', (v) => {
        selected.name = v;
        saveTemplates();
      })));
      children.push(row('Colors', renderColors(selected, saveTemplates)));
      children.push(row('Image', el('div', {
        class: 'pi-file-picker', id: 'template-image', 'data-accept': '.png,.jpg,.jpeg,.bmp,.gif',
        'data-placeholder': 'No image', 'data-select-title': 'Select image', 'data-clear-title': 'Clear image'
      })));
      children.push(row('Flash', textInput(selected.flashMs, 'off (ms)', (v) => {
        const parsed = parseInt(v, 10);
        selected.flashMs = Number.isFinite(parsed) && parsed > 0 ? parsed : null;
        saveTemplates();
      }, 'number'), 'Show then hide for this many ms each (minimum 250). Empty = no flashing.'));
      children.push(row('Flashes', flashTargetSelect(selected, saveTemplates),
        'Which part blinks while flashing. Background and Everything switch to black while off.'));
    } else {
      children.push(el('span', {
        class: 'pi-rule__hint', text: 'No templates yet. A template bundles colors, image and flash so rules can share one look.'
      }));
    }

    host.replaceChildren(...children);

    if (selected) {
      queueMicrotask(() => {
        SCPI?.ui?.filePicker?.createFilePicker?.({
          rootId: 'template-image',
          placeholderText: 'No image',
          initialValue: selected.imagePath || '',
          onValueChanged: (path) => {
            selected.imagePath = path || null;
            saveTemplates();
          }
        });
      });
    }
  }

  // Name edits are applied when the field loses focus so the rule selects are not rebuilt on every keystroke.
  document.getElementById('templates')?.addEventListener('change', (event) => {
    if (event.target?.matches?.('input[type="text"]')) {
      renderRules();
    }
  });

  let setTemplates = null;
  if (globalThis.SDPIComponents?.useGlobalSettings) {
    const [getTemplates, set] = globalThis.SDPIComponents.useGlobalSettings(
      'formatTemplates',
      (value) => {
        const next = Array.isArray(value) ? value : [];
        if (JSON.stringify(next) !== JSON.stringify(state.templates)) {
          state.templates = next;
          renderTemplates();
          renderRules();
        }
      });
    setTemplates = set;
    Promise.resolve(getTemplates()).then((value) => {
      state.templates = Array.isArray(value) ? value : [];
      renderTemplates();
      renderRules();
    }).catch(() => {
    });
  }

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
  if (TIMER) {
    useSetting('startRule', (v) => {
      state.startRule = v && typeof v === 'object' ? v : {op: 'always'};
      state.startRule.op = 'always';
      renderRules();
    });
    useSetting('stopRule', (v) => {
      state.stopRule = v && typeof v === 'object' ? v : {op: 'always'};
      state.stopRule.op = 'always';
      renderRules();
    });
  }

  SCPI?.ui?.filePicker?.createFilePicker?.({
    rootId: 'audioFilePicker',
    placeholderText: 'No file selected',
    settingsKey: 'clickSoundPath'
  });

  SCPI?.util?.onDocumentReady?.(() => {
    renderTemplates();
    renderRules();
    SCPI?.bus?.sendOnce?.('pi.connected', 'propertyInspectorConnected');
  });

  // #endregion
})();
