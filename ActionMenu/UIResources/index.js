const totalwidth = 500; // full width of actionmenu in pixels
const mid = 250; // half width in screen px
const maxdist = 0.15; // 'inner' joystick zone, normalized
const deadzone = 0.5; // normalized
const pi = Math.PI;
const pi2 = 2 * Math.PI;
const clamp = (min, max, v) => Math.min(max, Math.max(min, v));

const $actionmenu = document.getElementById("actionmenu");
const $joystick = $actionmenu.getElementsByClassName("joystick")[0];
const $sector = $actionmenu.getElementsByClassName("sector")[0];
const $enabled_sectors = $actionmenu.getElementsByClassName("enabled_sectors")[0];
const $separators = $actionmenu.getElementsByClassName("separators")[0];
const $inside = $actionmenu.getElementsByClassName("inside")[0];
const $items = $actionmenu.getElementsByClassName("items")[0];

var quickmenu_active = false;
var menu_name;
var menu;
var menus = {}; // dynamically loaded
var settings = {}; // dynamically loaded
var breadcrumb = []; // list of entered menu
var sectors = 0; // number of items in the menu
var selected_sector = null;
var gameData = {};
var active_widget = null;
var wait_joystick_recenter = false; // prevent users to select items by mistake for certain widgets
var was_in_deadzone = false;
var update_to_items = {}; // items registry to sync game<>menu state


function handle_direction(x, y) { // values between -1 and +1
	if (!quickmenu_active) return;

	const dist = Math.sqrt(x*x + y*y);
	const is_in_deadzone = dist <= deadzone;

	if (wait_joystick_recenter) {
		if (is_in_deadzone)
			wait_joystick_recenter = false;
		else
			return;
	}

	const f = active_widget?.handle_direction
		? active_widget.handle_direction // widget has taken over
		: handle_direction_main;

	f(x, y, dist);
	was_in_deadzone = is_in_deadzone;
}

function sector_rotation(sector) {
	const rounded = pi + (sector - 0.5) * pi2 / sectors;
	return rounded * 180 / pi;
}

function refresh_selection_sector(selected_sector) {
	const rounded_angle = sector_rotation(selected_sector);
	$sector.style.transform = `rotate(${rounded_angle}deg)`;
}

function handle_direction_main(x, y, dist) {
	const old_selected_sector = selected_sector;

	if (dist >= deadzone) {
		$sector.style.display = 'block';
		const angle = 2 * (pi - Math.atan(x / ( y + dist )));
		selected_sector = Math.round( angle * sectors / pi2 ) % sectors;
		refresh_selection_sector(selected_sector);
	}
	else { // deadzone = no selection
		if (settings.flick_selection && !was_in_deadzone && selected_sector != null) {
			handle_click_main(); // magics
			return;
		}
		$sector.style.display = 'none';
		selected_sector = null;
	}

	$joystick.style.left = 100*(0.5 + maxdist * x) + '%';
	$joystick.style.top  = 100*(0.5 + maxdist * y) + '%';


	if (selected_sector != null && old_selected_sector != selected_sector) {
		appcall("PlayCoreUiSound", "Hover");
		appcall("vibrateHand", 0, 0.1, 10, 1); // delay, duration, frequency, amplitude
	}
}

function handle_click() {
	if (!quickmenu_active) return;

	if (active_widget?.handle_click)
		return active_widget.handle_click();

	return handle_click_main();
}

const virtual_back_item = {
	"name": "Back",
	"icon": "icon_back.svg",
	"action": {"type": "back"},
}

function handle_click_main() {
	const item = selected_sector != null
		? menu[selected_sector]
		: (settings.boring_back_button ? null : virtual_back_item );

	if (item == null) return;
	//console.log(['click selected_sector', selected_sector, item.name, item]);

	const $item = selected_sector != null ? $items.childNodes[selected_sector] : $inside;

	const action = item.action;
	let action_toggle = false;
	const is_enabled = !!item.enabled;

	switch (action.type) {
		case 'menu': {
			const current_menu = menu_name;
			load_menu(action.menu);
			breadcrumb.push(current_menu);
			break;
		}

		case 'dynamic menu': {
			const current_menu = menu_name;
			load_dynamic_menu(action.menu);
			breadcrumb.push(current_menu);
			break;
		}

		case 'back': {
			const last_menu_name = breadcrumb.pop();
			if (last_menu_name != undefined) // if fail: main menu probably
				load_menu(last_menu_name);
			break;
		}

		case 'system call': {
			const args = action.event_arguments ?? [];
			appcall(action.event, ...args);
			action_toggle = !!action.toggle;
			break;
		}

		case 'set melon preference': {
			const new_value = item.enabled ? 0 : (action.value ?? 1);
			engine.call("SetMelonPreference", action.parameter, String(new_value));
			action_toggle = action?.toggle;
			break;
		}

		case 'callback': {
			switch (action.control) {
				case undefined:
				case null: {
					engine.call("ItemCallback", action.parameter)
					break;
				}

				case 'toggle': {
					action_toggle = true;
					engine.call("ItemCallback_bool", action.parameter, !item.enabled);
					break;
				}

				case 'impulse': {
					if (control_type_impulse(item, action,
						v => engine.call("ItemCallback_bool", action.parameter, v))
						)
						return;
					break;
				}

				case 'radial': {
					control_type_radial(item, action,
						v => engine.call("ItemCallback_float", action.parameter, v));
					break;
				}

				case 'joystick_2d':
				case 'input_vector_2d': {
					control_type_2d(item, action,
						(x, y) => engine.call("ItemCallback_float_float", action.parameter, x, y));
					break;
				}

				default:
					throw `unsupported control type: ${action.control}`;
			}
			break;
		}

		case 'avatar parameter': {
			switch (action.control) {
				case 'toggle': {
					action_toggle = true;
					const new_value = item.enabled ? 0 : (action.value ?? 1);
					appcall("AppChangeAnimatorParam", action.parameter, new_value);
					break;
				}

				case 'impulse': {
					if (control_type_impulse(item, action,
						v => appcall("AppChangeAnimatorParam", action.parameter, v))
						)
						return;
					break;
				}

				case 'radial': {
					control_type_radial(item, action,
						v => appcall("AppChangeAnimatorParam", action.parameter, v));
					break;
				}

				case 'joystick_2d':
				case 'input_vector_2d': {
					control_type_2d(item, action, (denormalized_x, denormalized_y) => {
						appcall("AppChangeAnimatorParam", action.parameter + '-x', denormalized_x);
						appcall("AppChangeAnimatorParam", action.parameter + '-y', denormalized_y);
					});
					break;
				}

				default:
					throw `unsupported control type: ${action.control}`;
			}
			break;
		}

		default:
			throw `Unknown action: ${action.type} item ${item.name}`;
	}

	if (action.exclusive_option) {
		clear_all_enabled_sectors(item.action?.parameter);
		menu.forEach(i => {
			if (i != item && i.action?.parameter == action?.parameter)
				i.enabled = false;
		});
	}
	if (action_toggle)
		set_item_enabled(selected_sector, item, !is_enabled);

	if ($item?.parentNode != null)
		trigger_animation($item, "animated-item");

	appcall("PlayCoreUiSound", "Click");
}

function control_type_impulse(item, action, set_value) {
	if (item.enabled) return true; // prevent spam
	const sector = selected_sector;
	set_item_enabled(sector, item, true);
	set_value(action.value ?? 1);
	setTimeout(() => {
		if (!item.enabled) return;
		set_item_enabled(sector, item, false);
		set_value(action.default_value ?? 0);
		appcall("PlayCoreUiSound", "Click");
	}, (action.duration ?? 1) * 1000);
}

function control_type_radial(item, action, set_value) {
	const min_value = action.min_value ?? 0;
	const max_value = action.max_value ?? 1;
	const delta = max_value - min_value;
	const start_value = ((action.default_value ?? 0) - min_value) / delta;
	widget_radial.start(item, start_value, (v) => {
		const denormalized = v * delta + min_value;
		set_value(denormalized);
		action.default_value = v; // kinda cheating but works
	});
	wait_joystick_recenter = true;
}

function control_type_2d(item, action, set_values) {
	const min_value_x = action.min_value_x ?? 0;
	const max_value_x = action.max_value_x ?? 1;
	const delta_x = max_value_x - min_value_x;
	const start_value_x = ((action.default_value_x ?? 0) - min_value_x) / delta_x;
	// TODO: we should restore from the previously set value

	const min_value_y = action.min_value_y ?? 0;
	const max_value_y = action.max_value_y ?? 1;
	const delta_y = max_value_y - min_value_y;
	const start_value_y = ((action.default_value_y ?? 0) - min_value_y) / delta_y;

	if (action.control == 'joystick_2d') {
		widget_j2d.start(item, start_value_x, start_value_y, (x, y) => {
			const denormalized_x = x * delta_x + min_value_x;
			const denormalized_y = y * delta_y + min_value_y;
			set_values(denormalized_x, denormalized_y);
			action.default_value_x = denormalized_x; // kinda cheating but works
			action.default_value_y = denormalized_y;
		});
	}
	else if (action.control == 'input_vector_2d') {
		const delta_scale = 0.01;
		let last_value_x = start_value_x;
		let last_value_y = start_value_y;

		// always start in the middle because it works in relative coords
		widget_j2d.start(item, 0.5, 0.5, (x, y) => {
			// reconvert from 0,1 to -1,+1 and scale
			const denormalized_x = clamp(min_value_x, max_value_x,
				last_value_x + delta_scale * (2*x - 1) * delta_x);
			const denormalized_y = clamp(min_value_y, max_value_y,
				last_value_y + delta_scale * (2*y - 1) * delta_y);
			set_values(denormalized_x, denormalized_y);
			last_value_x = denormalized_x;
			last_value_y = denormalized_y;
			action.default_value_x = last_value_x;
			action.default_value_y = last_value_y;
		});
	}
	wait_joystick_recenter = true;
}

function back_from_widget() {
	trigger_animation($inside, "animated-menu");

	if (settings.flick_selection) {
		selected_sector = null;
		wait_joystick_recenter = true; // security
	}

	active_widget = null; // back
}

document.addEventListener('mousemove', (event) => {
	if (settings.in_vr) return;
	let x = (event.clientX - mid);
	let y = (event.clientY - mid);
	const dist = Math.sqrt(x*x + y*y);

	// normalized and clamped to distance 1
	const distnorm = dist / totalwidth;
	if (distnorm > maxdist) {
		x /= dist;
		y /= dist;
	} else {
		const scale = maxdist * totalwidth;
		x /= scale;
		y /= scale;
	}

	handle_direction(x, y);
});

document.addEventListener('mouseup', (event) => {
	if (settings.in_vr || event.button != 0) return;
	handle_click();
});


function appcall(type, arg1, arg2, arg3, arg4) {
	// yes we need to convert all to string because they decided one fits all
	arg1 = arg1?.toString() || null;
	arg2 = arg2?.toString() || null;
	arg3 = arg3?.toString() || null;
	arg4 = arg4?.toString() || null;
	// yes this function needs all those args even if they're null
	//console.log("CVRAppCallSystemCall", type, arg1, arg2, arg3, arg4);
	engine.call("CVRAppCallSystemCall", type, arg1, arg2, arg3, arg4);
}

function set_item_enabled(sector, item, new_value) {
	item.enabled = new_value;
	show_item_enabled(sector, item);
}

function show_item_enabled(sector, item) {
	if (sector == null || item == null) throw `sector ${sector} or item ${item} is null`;

	const action = item.action;
	item.enabled = item.enabled ?? false;
	if (item.enabled) {
		if (action.$enabled) return; // ignore
		const $n = $sector.cloneNode();
		const angle = sector_rotation(sector);
		$n.style.display = "block";
		$n.style.transform = `rotate(${angle}deg)`;
		$n.classList.add('enabled');
		$enabled_sectors.appendChild($n);
		action.$enabled = $n;
	}
	else if (action.$enabled) {
		if (action.$enabled.parentNode) // if user changed menu, skip this
			$enabled_sectors.removeChild(action.$enabled);
		action.$enabled = null;
	}
}

// WARN: this doesn't support Object in any of the values!
const action_to_update_key = (action) => 
	['type', 'event', 'event_arguments', 'parameter']
	.map(k => String(action[k]))
	.join('\0');

function on_game_value_update(update) {
	if (update_to_items.length == 0) return; // not init yet
	const k = action_to_update_key(update);
	const items = update_to_items[k];
	if (!items) return;

	items.forEach(item => {
		const action = item.action;
		if (action.toggle)
			item.enabled = (action.value ?? true) == update.value;
		['default_value', 'default_value_x', 'default_value_y'] // TODO: support more?
		.forEach(k => {
			const v = update[k];
			if (v != null)
			action[k] = v;
		});
	});

	// update current menu in case an item is currently visible
	menu.forEach((item, i) => {
		if (items.includes(item))
			show_item_enabled(i, item);
	})
}

function build_$item(item, i) {
	const $item = document.createElement('div');
	$item.className = "item";

	if (item.icon != null) {
		const $icon = document.createElement('img');
		$icon.src = item.icon;
		$icon.className = "icon";
		$item.appendChild($icon);
	}

	if (item.name != null) {
		const $label = document.createElement('div');
		$label.innerHTML = item.name;
		$label.className = "label";
		$item.appendChild($label);
	}

	if (i != null && item.enabled)
		show_item_enabled(i, item);

	return $item;
}

function clear_all_enabled_sectors(parameter) {
	menu.forEach(item => {
		const action = item.action;
		if (parameter != null && item.action?.parameter != parameter)
			return; // skip if parameter is provided and match
		if (action?.$enabled != null) {
			if (action.$enabled.parentNode != null)
				$enabled_sectors.removeChild(action.$enabled);
			delete action.$enabled;
		}
	})
}

function load_menu(name) {
	if (menus[name] == null) throw `Menu ${name} not found`;
	menu = menus[name];

	$items.innerHTML = '';
	$enabled_sectors.innerHTML = '';
	$separators.innerHTML = '';
	clear_all_enabled_sectors();

	menu_name = name;
	sectors = menu.length;
	selection_sector_set(sectors);
	refresh_selection_sector(selected_sector);

	// build items
	menu.forEach((item, i) => {
		// draw separating line
		const sector_angle = (i + 0.5) * 360. / sectors;
		const $sep = document.createElement('div');
		$sep.className = "separator";
		$sep.style.transform = `translate(-50%, 0px) rotate(${sector_angle}deg)`;
		$separators.appendChild($sep);

		// draw item
		const label_angle = 0.5*pi + i * pi2 / sectors;
		const x = mid * (1 + 0.71 * Math.sin(label_angle));
		const y = mid * (1 + 0.71 * Math.cos(label_angle));

		const $item = build_$item(item, i);
		$item.style.top  = x +'px';
		$item.style.left = y +'px';
		trigger_animation($item, "animated-item");
		$items.appendChild($item);
	});

	// middle back button
	if (!settings.boring_back_button && menu_name != "main") {
		const $item = build_$item(virtual_back_item);
		$item.style.left = $item.style.top = mid +'px';
		$items.appendChild($item);
	}

	// animation weeeeeeee
	trigger_animation($inside, "animated-menu");
}

function trigger_animation($el, animation) {
	$el.classList.add(animation);
	$el.addEventListener('animationend', (event) => {
		$el.classList.remove(animation);
	}, {'once': true});
}

function selection_sector_set(sectors) {
	const angle = pi2 / sectors;
	const clip_path = compute_radial_mask(angle);
	$sector.style.clipPath = `polygon(${clip_path})`;
}

function compute_radial_mask(angle) { // angle in radians
	const quadrant = Math.floor(2 * angle / pi) % 4;
	const x = 50 * (1 + Math.sin(angle)); // coordinates are computed in %
	const y = 50 * (1 + Math.cos(pi - angle));
	// we're computing a polygon mask to only show the visible arc of a circle
	let points = [];
	if (quadrant <= 1) {
		// 0.001 is necessary due to a graphical bug when y=0
		points = [ [50,0], [50,50], [x, y], [100, y+0.001], [100, 0] ];
	}
	// depending on angle we have to add more points to fit all sections of the circle
	else if (quadrant <= 2) {
		points = [ [50,0], [50,50], [x, y], [x, 100], [100, 100], [100, 0] ];
	}
	else {
		points = [ [50,0], [50,50], [x, y], [0, y], [0, 100], [100, 100], [100, 0] ];
	}

	// format as css clipPath string
	return points.map(([x, y]) => `${x}% ${y}%`).join(" , ");
}


/* radial widget */

const widget_radial = (function() {
	const $w = document.getElementById("widget-radial");
	const $arc = $w.getElementsByClassName("arc")[0];
	const $indicator = $w.getElementsByClassName("indicator")[0];
	const $center = $w.getElementsByClassName("center")[0];
	const $value = $w.getElementsByClassName("value")[0];
	const $inside = $w.getElementsByClassName("inside")[0];

	let last_angle = 0;

	const handle_click_radial = () => {
		$w.style.display = 'none';

		back_from_widget();
	}

	const handle_direction_radial = (set_value, x, y, dist) => {
		if (dist >= deadzone) {
			const divisor =  y + dist;
			let angle = Math.abs(divisor) <= 0.0001 // protection for division by 0
				? 0.0
				: (pi - 2 * Math.atan(x / divisor));

			// protect from unintended big change 0<>100%
			if (Math.abs(last_angle - angle) > pi) {
				// freeze only close to 0 or 360°
				if (angle <= pi/2)
					angle = pi2 - 0.001;
				else if (angle >= 3*pi/2)
					angle = 0;
			}
			last_angle = angle;

			widget_radial_set(angle);
			$indicator.style.left = 100 * (0.5 + maxdist * Math.sin(angle)) + '%';
			$indicator.style.top  = 100 * (0.5 + maxdist * Math.cos(pi - angle)) + '%';

			const value = angle / pi2;
			set_value(value); // output between 0 and 1
			$value.innerHTML = value_label(value);
		}
		// else: deadzone = no update
	}

	const widget_radial_set = (angle) => {
		const clip_path = compute_radial_mask(angle);
		$arc.style.clipPath = `polygon(${clip_path})`;
	}

	const value_label = value => Math.floor(value * 100) + "%";

	const start = (item, start_value, set_value) => { // takes normalized value (0 to 1)
		$w.style.display = 'block';

		$center.innerHTML = "";
		const $item = build_$item(item);
		$center.appendChild($item);

		const handle_direction = (x, y, dist) => handle_direction_radial(set_value, x, y, dist);
		const angle = pi2 * start_value
		widget_radial_set(angle);
		last_angle = angle;
		$value.innerHTML = value_label(start_value);
		active_widget = {
			handle_direction: handle_direction,
			handle_click: handle_click_radial,
		};

		$indicator.style.left = $indicator.style.top = '50%'; // start in middle
		trigger_animation($inside, "animated-menu");
	}


	return {
		'start': start,
	};
})();


/* joystick 2D widget */

const widget_j2d = (function() {
	const $w = document.getElementById("widget-j2d");
	const $joystick = $w.getElementsByClassName("joystick")[0];
	const $center = $w.getElementsByClassName("center")[0];
	const $inside = $w.getElementsByClassName("inside")[0];
	const $triangles = $w.getElementsByClassName("triangles")[0];

	const handle_direction_joystick_2d = (set_value, x, y, dist) => {
		const angle = y <= -1 // protection for division by 0
			? pi2 - 0.001
			: (pi - 2 * Math.atan(x / ( y + dist )));

		values_to_joystick(x, y);

		const triangles = $triangles.childNodes.length;
		Array.prototype.forEach.call($triangles.childNodes, ($t, i) => {
			const angle =  i * pi2 / triangles;
			const tx = Math.sin(angle);
			const ty = Math.cos(angle);
			const dot = x * tx + y * ty; // between -1 and +1

			const size = 0.1*mid + 0.35*mid * Math.max( 0, dot );
			$t.style.borderLeft = $t.style.borderRight = size +'px solid transparent';
			$t.style.borderBottom = size +'px solid #dac024';
		});

		set_value(0.5 * (1 + x), 0.5 * (1 + y)); // convert range -1,+1 to 0,1
	}

	const values_to_joystick = (x, y) => { // expecting values -1,+1
		$joystick.style.left = 100*(0.5 + maxdist * x) + '%';
		$joystick.style.top  = 100*(0.5 + maxdist * y) + '%';
	};

	const handle_click_joystick_2d = () => {
		$w.style.display = 'none';

		back_from_widget();
	}

	const start = (item, start_value_x, start_value_y, set_value) => { // takes normalized values x,y both (0 to 1)
		$w.style.display = 'block';

		$triangles.innerHTML = "";

		$center.innerHTML = "";
		const $item = build_$item(item);
		$center.appendChild($item);

		const triangles = 4;
		[...Array(triangles).keys()].forEach((i) => {
			const $t = document.createElement('div');
			$t.className = "triangle";
			const angle = i * pi2 / triangles;
			$t.style.top  = 50 + 35 * Math.cos(angle) + '%';
			$t.style.left = 50 + 35 * Math.sin(angle) + '%';
			$t.style.transform = `translate(-50%, -50%) rotate(${(pi - angle) * 180 / pi}deg)`;
			$triangles.appendChild($t);
		});

		const handle_direction = (x, y, dist) => handle_direction_joystick_2d(set_value, x, y, dist);
		active_widget = {
			handle_direction: handle_direction,
			handle_click: handle_click_joystick_2d,
		};

		// for joystick start position: convert normalized -> denormalized
		values_to_joystick(start_value_x * 2 - 1, start_value_y * 2 - 1);
		trigger_animation($inside, "animated-menu");
	}

	return {
		'start': start,
	}
})();


/* dispatchers */

function load_action_menu(_menu, _settings) {
    menus = _menu.menus;
	settings = _settings ?? {};
	console.log('load_action_menu menus:', Object.keys(_menu.menus));
	console.log('load_action_menu settings:', JSON.stringify(settings));

	if (settings.flick_selection)
		wait_joystick_recenter = true;

	// add a nice back button if requested
	if (settings.boring_back_button) {
		for (const [name, m] of Object.entries(menus)) {
			if (name == "main") continue;
			m.unshift(virtual_back_item);
		}
	}

	// need an item registry to sync game<>menu update system
	Object.values(menus).forEach(m => {
		m.forEach(item => {
			const k = action_to_update_key(item.action);
			const xs = update_to_items[k] = update_to_items[k] ?? [];
			xs.push(item);
		});
	});

	load_menu("main");
}

(function () {
	let waiting_for_menu;

	window.load_dynamic_menu = (name) => {
		waiting_for_menu = name;
		engine.call('RequestDynamicMenu', name);
	};

	engine.on('DynamicMenuData', (_menus) => {
		if (!waiting_for_menu) throw `Not waiting for a menu ${waiting_for_menu}`;

		const m = JSON.parse(_menus)
		if (m[waiting_for_menu] == null) throw `Waiting for ${waiting_for_menu} but not found`;

		Object.keys(m).forEach(k => {
			menus[k] = m[k];
		});

		const new_menu = waiting_for_menu;
		waiting_for_menu = null;
		load_menu(new_menu);
	});
})();

(function() {
	let last_trigger = false;

	engine.on('InputData', (_content) => {
		gameData = JSON.parse(_content);

		const joyvec = gameData.joystick;
		handle_direction(joyvec.x, -joyvec.y); // we invert y

		const trigger = gameData.trigger > 0.9;
		if (trigger && !last_trigger)
			handle_click();
		last_trigger = trigger;
	});
})();

engine.on('LoadActionMenu', (_content, _settings) => {
	load_action_menu(JSON.parse(_content), JSON.parse(_settings));
});

engine.on('ToggleActionMenu', (show) => {
	quickmenu_active = show;
});

engine.on('GameValueUpdate', (_update) => {
	on_game_value_update(JSON.parse(_update).action);
});


/* start */

if (window.navigator.appVersion != undefined) { // browser only
	fetch('actionmenu.json')
	.then((data) =>  data.json())
	.then((j) => {
		load_action_menu(j, {
			in_vr: false,
			boring_back_button: false,
			flick_selection: false,
		});
	});
	quickmenu_active = true;
} else {
	engine.trigger('ActionMenuReady');
}
