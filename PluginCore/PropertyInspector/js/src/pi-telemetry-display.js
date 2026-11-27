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

  // Travel page: no attribute picker; rules can test the measurement (set by Travel.html).
  const TRAVEL = globalThis.SCPI_TELEMETRY_TRAVEL === true;

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
    flags: [],
    defaultRule: {op: 'always'},
    startRule: {op: 'always'},
    runningRule: {op: 'always'},
    stoppedRule: {op: 'always'},
    armedRule: {op: 'always'},
    recordingRule: {op: 'always'},
    savedRule: {op: 'always'},
    discardedRule: {op: 'always'}
  };

  // Travel page: the looks of the measurement states, shown unless an Unavailable rule matches. They keep press actions.
  const TRAVEL_LOOKS = [
    {key: 'startRule', id: 'start', title: 'Start (no measurement yet; pressing starts one)', placeholder: 'Start'},
    {key: 'runningRule', id: 'running', title: 'Running (pressing stops the measurement)', placeholder: '{elapsed}\\n{distance}'},
    {key: 'stoppedRule', id: 'stopped', title: 'Stopped (frozen values; pressing starts a new measurement)', placeholder: '{elapsed}\\n{distance}'}
  ];

  // Deceleration page: looks of the recording states (no conditions and no press actions; the rules keep the press).
  const DECEL_STATES = [
    {key: 'armedRule', id: 'armed', title: 'Armed (waiting for the throttle to be released)', placeholder: 'Armed\\nrelease\\nthrottle'},
    {key: 'recordingRule', id: 'recording', title: 'Recording (the ship is slowing down)', placeholder: 'REC\\n{speed}'},
    {key: 'savedRule', id: 'saved', title: 'Saved (shown for a few seconds after a run was saved)', placeholder: 'Saved\\n{runs}'},
    {key: 'discardedRule', id: 'discarded', title: 'Discarded (shown for a few seconds after a run was dropped)', placeholder: 'Discarded\\n{reason}'}
  ];

  // Attributes the plugin reports plus the pseudo attributes of this page; flags are added for conditions.
  let baseFields = [];
  let fields = [];

  function flagName(flag) {
    return String(flag?.name || '').trim() || '(unnamed)';
  }

  function rebuildFields() {
    const flagFields = ADVANCED
      ? state.flags.map((f) => ({
        path: `flag.${f.id}`, label: flagName(f), group: 'Flags', kind: 'Boolean', isInteger: false, enumValues: null
      }))
      : [];
    fields = baseFields.concat(flagFields);
  }

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
    if (TRAVEL) {
      TRAVEL_LOOKS.forEach((s) => save(s.key, state[s.key]));
    }
    if (DECEL) {
      DECEL_STATES.forEach((s) => save(s.key, state[s.key]));
    }
  }

  // #endregion

  // #region Helpers

  const {el, row, textInput, selectInput, flashTargetSelect} = SCPI.ui.form;

  function currentField(path = state.fieldPath) {
    return fields.find((f) => f.path === path) || null;
  }

  function fieldKind(path = state.fieldPath) {
    return String(currentField(path)?.kind || '').toLowerCase();
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
    baseFields = DECEL ? real.concat(DECEL_FIELDS) : UEC ? real.concat(UEC_FIELDS) : TRAVEL ? real.concat(TRAVEL_FIELDS) : real;
    rebuildFields();
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
    mode: document.getElementById('decelMode'),
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
      mode: decelInputs.mode?.value || null,
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
    if (decelInputs.mode) decelInputs.mode.value = d.mode || '';
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
    if (TRAVEL) {
      const looks = TRAVEL_LOOKS.map((s) => ({s, host: document.getElementById(s.key)}));
      if (!list || looks.some((l) => !l.host)) {
        return;
      }

      list.replaceChildren(...state.rules.map((rule, index) => renderRuleCard(rule, index, false)));
      looks.forEach(({s, host}) => host.replaceChildren(renderRuleCard(state[s.key], -1, true, {
        id: s.id, title: s.title, placeholder: s.placeholder
      })));
      return;
    }

    const defaults = document.getElementById('defaultRule');
    if (!list || !defaults) {
      return;
    }

    list.replaceChildren(...state.rules.map((rule, index) => renderRuleCard(rule, index, false)));
    defaults.replaceChildren(renderRuleCard(state.defaultRule, -1, true));

    if (DECEL) {
      DECEL_STATES.forEach((s) => {
        document.getElementById(s.key)?.replaceChildren(renderRuleCard(state[s.key], -1, true, {
          id: s.id,
          title: s.title,
          placeholder: s.placeholder,
          noPress: true,
          hint: 'Use {state}, {speed} (m/s), {runs}, {reason}, {label} or {value:attribute.path}; \\n starts a new line. Empty = '
            + s.placeholder + '.'
        }));
      });
    }
  }

  // `section` (Travel looks) is a card without conditions that still has sound and press actions.
  function renderRuleCard(rule, index, isDefault, section = null) {
    const idPrefix = section ? `rule-${section.id}` : isDefault ? 'rule-default' : `rule-${index}`;
    const title = section ? section.title : isDefault ? 'Default (no rule matches / no data)'
      : TRAVEL ? `Unavailable when (rule ${index + 1})` : `Rule ${index + 1}`;
    const showSound = !isDefault || !!section;
    // Pressing an unavailable key does nothing, so its rules have no press actions.
    const showPress = !(TRAVEL && !section) && !section?.noPress;
    const textPlaceholder = section?.placeholder || (TRAVEL ? 'Unavailable' : null);

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

    card.appendChild(row('Text', textInput(rule.text, textPlaceholder || (UEC ? '{value}\\n{change}' : '{label}\\n{value}'), (v) => {
      rule.text = v;
      saveRules();
    }), section?.hint ? section.hint : TRAVEL
      ? `Use {state}, {elapsed} (min), {distance}, {label} or {value:attribute.path}; \\n starts a new line. Empty = ${textPlaceholder}.`
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
      'Colors, image and flash from a shared format template made on the Config key. Anything set on this rule itself overrides the template.'));
    card.appendChild(row('Colors', renderColors(rule)));
    card.appendChild(row('Flash', textInput(rule.flashMs, 'off (ms)', (v) => {
      const parsed = parseInt(v, 10);
      rule.flashMs = Number.isFinite(parsed) && parsed > 0 ? parsed : null;
      saveRules();
    }, 'number'), 'Show the content for this many ms, then hide it for the same time, repeating. Minimum 250. Empty = no flashing.'));
    card.appendChild(row('Flashes', flashTargetSelect(rule, saveRules), 'Which part blinks while flashing. Background and Everything switch to black while off.'));
    card.appendChild(row('Sets flags', renderSetFlags(rule),
      'Flags to switch on or off once when this rule becomes active. Flags are created on the Config key.'));
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

  function flagSelect(value, onChange) {
    return selectInput([['', 'Select flag...'], ...state.flags.map((f) => [f.id, flagName(f)])], value || '',
      onChange, 'pi-rule__select pi-rule__attribute');
  }

  // Flags a rule switches when it becomes the active one (applied by the plugin once per activation).
  function renderSetFlags(rule) {
    const wrap = el('div', {class: 'pi-rule__actions-list'});

    if (state.flags.length === 0 && !(rule.setFlags || []).length) {
      wrap.appendChild(el('span', {class: 'pi-rule__hint', text: 'No flags yet. Create them on the Config key.'}));
      return wrap;
    }

    (rule.setFlags || []).forEach((assignment, index) => {
      const line = el('div', {class: 'pi-rule__condition-row'});
      line.appendChild(flagSelect(assignment.flag, (id) => {
        assignment.flag = id || null;
        saveRules();
      }));
      line.appendChild(selectInput([['true', 'On'], ['false', 'Off']], String(assignment.value !== false), (v) => {
        assignment.value = v === 'true';
        saveRules();
      }));
      line.appendChild(el('button', {
        class: 'file-picker-clear', type: 'button', title: 'Remove flag', text: 'X',
        onClick: () => {
          rule.setFlags.splice(index, 1);
          saveRules();
          renderRules();
        }
      }));
      wrap.appendChild(line);
    });

    wrap.appendChild(el('button', {
      class: 'pi-button', type: 'button', text: 'Add flag',
      onClick: () => {
        rule.setFlags = (rule.setFlags || []).concat({flag: null, value: true});
        saveRules();
        renderRules();
      }
    }));

    return wrap;
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
    return SCPI.ui.form.colors(rule, onChange);
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
      const isFlag = action.flag !== undefined && action.flag !== null;
      line.appendChild(selectInput([['function', 'Star Citizen action'], ['flag', 'Flag']], isFlag ? 'flag' : 'function', (kind) => {
        action.function = null;
        action.flag = kind === 'flag' ? '' : null;
        action.flagMode = kind === 'flag' ? 'toggle' : null;
        saveRules();
        renderRules();
      }));
      line.appendChild(isFlag
        ? renderFlagAction(action)
        : renderFunctionPicker(action, `${idPrefix}-action-${index}`, () => saveRules(), false));
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
      text: 'Actions run in order when the key is pressed (a single action without delay can be held). Flag actions also work without telemetry.'
    }));

    return wrap;
  }

  // An action that switches a flag on or off, or toggles it.
  function renderFlagAction(action) {
    return el('div', {class: 'pi-rule__condition-row'}, [
      flagSelect(action.flag, (id) => {
        action.flag = id;
        saveRules();
      }),
      selectInput([['toggle', 'Toggle'], ['on', 'Set on'], ['off', 'Set off']], action.flagMode || 'toggle', (mode) => {
        action.flagMode = mode;
        saveRules();
      })
    ]);
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

  // #region Format templates and flags (created on the Config key; kept in the plugin's global settings)

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

  // Reads one list of the plugin's global settings and calls `onList` now and whenever it changes.
  function watchGlobalList(key, onList) {
    if (!globalThis.SDPIComponents?.useGlobalSettings) {
      return;
    }

    let last = null;
    const apply = (value) => {
      const next = Array.isArray(value) ? value : [];
      const json = JSON.stringify(next);
      if (json !== last) {
        last = json;
        onList(next);
      }
    };

    const [get] = globalThis.SDPIComponents.useGlobalSettings(key, apply);
    Promise.resolve(get()).then(apply).catch(() => {
    });
  }

  watchGlobalList('formatTemplates', (list) => {
    state.templates = list;
    renderRules();
  });
  watchGlobalList('flags', (list) => {
    state.flags = list;
    rebuildFields();
    renderRules();
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
  if (DECEL) {
    DECEL_STATES.forEach((s) => {
      useSetting(s.key, (v) => {
        state[s.key] = v && typeof v === 'object' ? v : {op: 'always'};
        state[s.key].op = 'always';
        renderRules();
      });
    });
  }
  if (TRAVEL) {
    TRAVEL_LOOKS.forEach((s) => {
      useSetting(s.key, (v) => {
        state[s.key] = v && typeof v === 'object' ? v : {op: 'always'};
        state[s.key].op = 'always';
        renderRules();
      });
    });
  }

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
