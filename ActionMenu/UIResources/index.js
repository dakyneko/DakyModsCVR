const totalwidth = 500; // full width of actionmenu in pixels
const mid = 250; // half width in screen px
const maxdist = 0.15; // 'inner' joystick zone, normalized
const deadzone = 0.5; // normalized
const pi = Math.PI;
const pi2 = 2 * Math.PI;

const $actionmenu = document.getElementById("actionmenu");
const $background = $actionmenu.getElementsByClassName("background")[0];
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
var breadcrumb = []; // list of entered menu
var sectors = 5; // number of items in the menu
var selected_sector = null;
var last_leftTrigger = false;
var gameData = {};
var active_widget = null;
var in_vr = false;
var wait_joystick_recenter = false; // prevent users to input values by mistake for certain widgets


function handle_direction(x, y) { // values between -1 and +1
	if (!quickmenu_active) return;

	const dist = Math.sqrt(x*x + y*y);

	if (wait_joystick_recenter) {
		if (dist <= deadzone)
			wait_joystick_recenter = false;
		else
			return;
	}

	const f = active_widget?.handle_direction
		? active_widget.handle_direction // widget has taken over
		: handle_direction_main;

	return f(x, y, dist);
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
	"name": "back",
	"action": {"type": "back"},
}

function handle_click_main() {
	const item = selected_sector != null ? menu[selected_sector] : virtual_back_item;
	console.log(['click selected_sector', selected_sector, item.name, item]);

	const $item = selected_sector != null ? $items.childNodes[selected_sector] : $inside;

	const action = item.action;
	let action_toggle = false;
	switch (action.type) {
		case 'menu':
			const current_menu = menu_name;
			load_menu(action.menu);
			breadcrumb.push(current_menu);
			break;

		case 'back':
			const last_menu_name = breadcrumb.pop();
			if (last_menu_name != undefined) // if fail: main menu probably
				load_menu(last_menu_name);
			break;

		case 'system call':
			appcall(action.event);
			action_toggle = action?.toggle;
			break;

		case 'avatar parameter':
			switch (action.control) {
				case 'radial':
					// TODO: restore value from cvr?
					const min_value = action.min_value ?? 0;
					const max_value = action.max_value ?? 1;
					const delta = max_value - min_value;
					const start_value = ((action.default_value ?? 0) - min_value) / delta;
					start_widget_radial(item, start_value, (v) => {
						const denormalized = v * delta + min_value;
						appcall("AppChangeAnimatorParam", action.parameter, denormalized);
					});
					trigger_animation($wr_inside, "animated-menu");
					wait_joystick_recenter = true;
					break;

				case 'joystick_2d':
					const min_value_x = action.min_value_x ?? 0;
					const max_value_x = action.max_value_x ?? 1;
					const delta_x = max_value_x - min_value_x;
					const start_value_x = ((action.default_value_x ?? 0) - min_value_x) / delta_x;

					const min_value_y = action.min_value_y ?? 0;
					const max_value_y = action.max_value_y ?? 1;
					const delta_y = max_value_y - min_value_y;
					const start_value_y = ((action.default_value_y ?? 0) - min_value_y) / delta_y;

					start_widget_joystick_2d(item, start_value_x, start_value_y, (x, y) => {
						const denormalized_x = x * delta_x + min_value_x;
						const denormalized_y = y * delta_y + min_value_y;
						appcall("AppChangeAnimatorParam", action.parameter + '-x', denormalized_x);
						appcall("AppChangeAnimatorParam", action.parameter + '-y', denormalized_y);
					});
					trigger_animation($wj2d_inside, "animated-menu");
					wait_joystick_recenter = true;
					break;

				case 'impulse':
					if (item.enabled) return; // prevent spam
					const sector = selected_sector;
					toggle_item_enabled(sector, item);
					appcall("AppChangeAnimatorParam", action.parameter, action.value ?? 1);
					setTimeout(() => {
						if (!item.enabled) return;
						toggle_item_enabled(sector, item);
						appcall("AppChangeAnimatorParam", action.parameter, action.default_value ?? 0);
						appcall("PlayCoreUiSound", "Click");
					}, (action.duration ?? 1) * 1000);
					break;

				case 'toggle':
					action_toggle = true;
					const new_value = item.enabled ? 0 : (action.value ?? 1);
					appcall("AppChangeAnimatorParam", action.parameter, new_value);
					break;

				default:
					throw `unsupported control type: ${action.control}`;
			}
			break;

		default:
			throw `Unknown action: ${action.type} item ${item.name}`;
	}

	if (action.exclusive_option) {
		clear_all_enabled_sectors();
		menu.forEach(i => {
			if (i != item)
				i.enabled = false;
		});
	}
	if (action_toggle)
		toggle_item_enabled(selected_sector, item);

	if ($item?.parentNode != null)
		trigger_animation($item, "animated-item");

	appcall("PlayCoreUiSound", "Click");
}

document.addEventListener('mousemove', (event) => {
	if (in_vr) return;
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
	if (in_vr || event.button != 0) return;
	handle_click();
});


function appcall(type, arg1, arg2, arg3, arg4) {
	// yes we need to convert all to string because they decided one fits all
	arg1 = arg1?.toString() || null;
	arg2 = arg2?.toString() || null;
	arg3 = arg3?.toString() || null;
	arg4 = arg4?.toString() || null;
	// yes this function needs all those args even if they're null
	console.log("CVRAppCallSystemCall", type, arg1, arg2, arg3, arg4);
	engine.call("CVRAppCallSystemCall", type, arg1, arg2, arg3, arg4);
}

function toggle_item_enabled(sector, item) {
	item.enabled = !(item.enabled ?? false);
	show_item_enabled(sector, item);
}

function show_item_enabled(sector, item) {
	if (sector == null || item == null) throw `sector ${sector} or item ${item} is null`;

	const action = item.action;
	item.enabled = item.enabled ?? false;
	if (item.enabled) {
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

function clear_all_enabled_sectors() {
	menu.forEach(item => {
		const action = item.action;
		if (action?.$enabled != null) {
			if (action.$enabled.parentNode != null)
				$enabled_sectors.removeChild(action.$enabled);
			delete action.$enabled;
		}
	})
}

function load_menu(name) {
	menu = menus[name];
	if (menu == null) throw `Menu ${name} not found`;

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
	{
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


/* radial widget */

const $widget_radial = document.getElementById("widget-radial");
const $wr_arc = $widget_radial.getElementsByClassName("arc")[0];
const $wr_indicator = $widget_radial.getElementsByClassName("indicator")[0];
const $wr_center = $widget_radial.getElementsByClassName("center")[0];
const $wr_value = $widget_radial.getElementsByClassName("value")[0];
const $wr_inside = $widget_radial.getElementsByClassName("inside")[0];

function start_widget_radial(item, start_value, set_value) { // takes normalized value (0 to 1)
	$widget_radial.style.display = 'block';

	$wr_center.innerHTML = "";
	const $item = build_$item(item);
	$wr_center.appendChild($item);

	const handle_direction = (x, y, dist) => handle_direction_radial(set_value, x, y, dist);
	widget_radial_set(pi2 * start_value);
	$wr_value.innerHTML = Math.floor(start_value * 100) + "%";
	active_widget = {
		handle_direction: handle_direction,
		handle_click: handle_click_radial,
	};

	$wr_indicator.style.left = $wr_indicator.style.top = '50%'; // start in middle
}

function handle_click_radial() {
	$widget_radial.style.display = 'none';

	active_widget = null; // back
	trigger_animation($inside, "animated-menu");
}

function handle_direction_radial(set_value, x, y, dist) {
	// TODO: add mechanism to disallow jumping from -1 to +1 at angle 0, protection

	if (dist >= deadzone) {
		const angle = y <= -1 // protection for division by 0
			? pi2 - 0.001
			: (pi - 2 * Math.atan(x / ( y + dist )));

		widget_radial_set(angle);
		$wr_indicator.style.left = 100 * (0.5 + maxdist * Math.sin(angle)) + '%';
		$wr_indicator.style.top  = 100 * (0.5 + maxdist * Math.cos(pi - angle)) + '%';

		const value = angle / pi2;
		set_value(value); // output between 0 and 1
		$wr_value.innerHTML = Math.floor(value * 100) + "%";
	}
	// else: deadzone = no update
}

function widget_radial_set(angle) {
	const clip_path = compute_radial_mask(angle);
	$wr_arc.style.clipPath = `polygon(${clip_path})`;
}

function selection_sector_set(sectors) {
	const angle = pi2 / sectors;
	const clip_path = compute_radial_mask(angle);
	$sector.style.clipPath = `polygon(${clip_path})`;
}

function compute_radial_mask(angle) { // angle in radians
	const quadrant = Math.floor(2 * angle / pi) % 4; // TODO: at 100% = NaN = the full circle disappear like it's 0%
	const x = 50 * (1 + Math.sin(angle)); // coordinates are computed in %
	const y = 50 * (1 + Math.cos(pi - angle));
	// we're computing a polygon mask to only show the visible arc of a circle
	let points = [];
	if (quadrant <= 1) {
		points = [ [50,0], [50,50], [x, y], [100, y], [100, 0] ];
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


/* 2D widget */

const $widget_j2d = document.getElementById("widget-j2d");
const $wj2d_joystick = $widget_j2d.getElementsByClassName("joystick")[0];
const $wj2d_center = $widget_j2d.getElementsByClassName("center")[0];
const $wj2d_inside = $widget_j2d.getElementsByClassName("inside")[0];
const $wj2d_triangles = $widget_j2d.getElementsByClassName("triangles")[0];

function start_widget_joystick_2d(item, start_value_x, start_value_y, set_value) {
	$widget_j2d.style.display = 'block';

	$wj2d_triangles.innerHTML = "";

	$wj2d_center.innerHTML = "";
	const $item = build_$item(item);
	$wj2d_center.appendChild($item);

	const triangles = 4;
	[...Array(triangles).keys()].forEach((i) => {
		const $t = document.createElement('div');
		$t.className = "triangle";
		const angle = i * pi2 / triangles;
		$t.style.top  = 50 + 35 * Math.cos(angle) + '%';
		$t.style.left = 50 + 35 * Math.sin(angle) + '%';
		$t.style.transform = `translate(-50%, -50%) rotate(${(pi - angle) * 180 / pi}deg)`;
		$wj2d_triangles.appendChild($t);
	});

	const handle_direction = (x, y, dist) => handle_direction_joystick_2d(set_value, x, y, dist);
	// TODO: handle default x y
	active_widget = {
		handle_direction: handle_direction,
		handle_click: handle_click_joystick_2d,
	};

	$wj2d_joystick.style.left = $wr_indicator.style.top = '50%'; // start in middle
}

function handle_direction_joystick_2d(set_value, x, y, dist) {
	const angle = y <= -1 // protection for division by 0
		? pi2 - 0.001
		: (pi - 2 * Math.atan(x / ( y + dist )));


	$wj2d_joystick.style.left = 100*(0.5 + maxdist * x) + '%';
	$wj2d_joystick.style.top  = 100*(0.5 + maxdist * y) + '%';

	const triangles = $wj2d_triangles.childNodes.length;
	Array.prototype.forEach.call($wj2d_triangles.childNodes, ($t, i) => {
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

function handle_click_joystick_2d() {
	$widget_j2d.style.display = 'none';

	active_widget = null; // back
	trigger_animation($inside, "animated-menu");
}


/* dispatchers */

function loadActionMenu(j) {
	console.log('fetched', typeof(j), Object.keys(j.menus));
    menus = j.menus;
	load_menu("main");
}

engine.on('ActionMenuData', (_content) => {
	gameData = JSON.parse(_content);
	//console.log(['ActionMenuData', _content]);

	const joyvec = gameData.joystick;
	handle_direction(joyvec.x, -joyvec.y); // we invert y

	const leftTrigger = gameData.trigger > 0.9;
	if (leftTrigger && !last_leftTrigger) handle_click();
	last_leftTrigger = leftTrigger;
});

engine.on('LoadActionMenu', (_content, inVr) => {
	loadActionMenu(JSON.parse(_content));
	in_vr = inVr;
});

engine.on('ToggleQuickMenu', (show) => {
	console.log(['ToggleQuickMenu', show]);
	quickmenu_active = show;
});


/* start */

if (window.navigator.appVersion != undefined) { // browser only
	fetch('actionmenu.json')
	.then((data) =>  data.json())
	.then((j) => {
		loadActionMenu(j);
	});
	quickmenu_active = true;
} else {
	engine.trigger('CVRAppActionActionMenuReady');
}
