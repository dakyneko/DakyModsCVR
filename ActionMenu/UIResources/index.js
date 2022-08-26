const totalwidth = 500;
const mid = 250; // half width of whole menu in screen px
const maxdist = 0.15; // deadzone in screen px
const deadzone = 0.5; // %
const pi = Math.PI;
const pi2 = 2 * Math.PI;

const $actionmenu = document.getElementById("actionmenu");
const $background = $actionmenu.getElementsByClassName("background")[0];
const $joystick = $actionmenu.getElementsByClassName("joystick")[0];
const $segment = $actionmenu.getElementsByClassName("segment")[0];
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


function handle_direction(x, y) { // values between -1 and +1
	if (!quickmenu_active) return;

	if (active_widget?.handle_direction)
		return active_widget.handle_direction(x, y);

	return handle_direction_main(x, y);
}

function handle_direction_main(x, y) {
	const dist = Math.sqrt(x*x + y*y);
	const old_selected_sector = selected_sector;

	if (dist >= deadzone) {
		$segment.style.display = 'block';
		const angle = 2 * (pi - Math.atan(x / ( y + dist )));
		selected_sector = Math.round( angle * sectors / pi2 ) % sectors;
		const rounded = selected_sector * pi2 / sectors;
		$segment.style.transform = `rotate(${rounded * 180 / pi}deg)`;
	}
	else { // deadzone = no selection
		$segment.style.display = 'none';
		selected_sector = null;
	}

	$joystick.style.left = 100*(0.5 + maxdist * x) + '%'; // denormalize
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

	const action = item.action;
	switch (action.type) {
		case 'menu':
			breadcrumb.push(menu_name);
			load_menu(action.menu); break;

		case 'back':
			var last_menu_name = breadcrumb.pop();
			if (last_menu_name != undefined) // if fail: main menu probably
				load_menu(last_menu_name);
			break;

		case 'system call':
			appcall(action.event);
			break;

		case 'avatar parameter':
			switch (action?.control) {
				case 'radial':
					// TODO: get start value from cvr
					// TODO: adjust output value range, -1 to +1 is only one possibility (=floats?)
					const start_value = 0
					start_widget_radial(item, start_value, (v) => appcall("AppChangeAnimatorParam", action.parameter, v));
					trigger_animation($wr_inside, "animated-menu");
					break;

				case 'trigger':
				default:
					appcall("AppChangeAnimatorParam", action.parameter, action.value);
					break;
			}
			break;

		default:
			throw Exception(`Unknown action: ${action.type} item ${item.name}`);
	}

	if (selected_sector != null)
		trigger_animation($items.childNodes[selected_sector], "animated-item");
	else
		trigger_animation($inside, "animated-menu");

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
	if (in_vr) return;
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

function build_$item(item, x, y) {
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

	return $item;
}

function load_menu(name) {
	menu = menus[name];
	if (menu == null) throw Exception(`Menu ${name} not found`);

	$items.innerHTML = '';
	$separators.innerHTML = '';

	menu_name = name;
	sectors = menu.length;

	menu.forEach((item, i) => {
		// draw separating line
		const sector_angle = (i + 0.5) * 360. / sectors;
		const $sep = document.createElement('div');
		$sep.className = "separator";
		$sep.style.transform = `translate(-50%, 0px) rotate(${sector_angle}deg)`;
		$separators.appendChild($sep);

		// draw item
		const label_angle = 0.5*pi + i * pi2 / sectors;
		const x = mid * (1 + 0.71 * Math.sin(label_angle)); // TODO: to fix
		const y = mid * (1 + 0.71 * Math.cos(label_angle));

		const $item = build_$item(item);
		$item.style.top  = `${x}px`;
		$item.style.left = `${y}px`;
		trigger_animation($item, "animated-item");
		$items.appendChild($item);
	});

	// middle back button
	{
		const $item = build_$item(virtual_back_item);
		$item.style.left = $item.style.top = `${mid}px`;
		$items.appendChild($item);
	}

	// animation weeeeeeee
	trigger_animation($inside, "animated-menu");

	// TODO: update css styles to fit new number of sectors ('region' etc)
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
const $wr_joystick = $widget_radial.getElementsByClassName("joystick")[0];
const $wr_center = $widget_radial.getElementsByClassName("center")[0];
const $wr_value = $widget_radial.getElementsByClassName("value")[0];
const $wr_inside = $widget_radial.getElementsByClassName("inside")[0];

function start_widget_radial(item, start_value, set_value) {
	$actionmenu.style.opacity = 0.5;
	$widget_radial.style.display = 'block';

	$wr_center.innerHTML = "";
	const $item = build_$item(item);
	$wr_center.appendChild($item);

	const handle_direction = (x, y) => handle_direction_radial(set_value, x, y);
	handle_direction(0, 1);
	active_widget = {
		handle_direction: handle_direction,
		handle_click: handle_click_radial,
	};
}

function handle_click_radial() {
	$actionmenu.style.opacity = 1;
	$widget_radial.style.display = 'none';

	active_widget = null; // back
	trigger_animation($inside, "animated-menu");
}

function handle_direction_radial(set_value, x, y) {
	const dist = Math.sqrt(x*x + y*y);
	// TODO: add mechanism to disallow jumping from -1 to +1 at angle 0, protection

	if (dist >= deadzone) {
		const angle = (pi - 2 * Math.atan(x / ( y + dist )));

		widget_radial_set(angle);
		$wr_joystick.style.left = 100 * (0.5 + maxdist * Math.sin(angle)) + '%';
		$wr_joystick.style.top  = 100 * (0.5 + maxdist * Math.cos(pi - angle)) + '%';

		const value = angle / pi - 1;
		set_value(value); // output between -1 and +1
		$wr_value.innerHTML = Math.floor(value * 100) + "%";
	}
	// else: deadzone = no update
}

function widget_radial_set(angle) { /* in rad */
	const quadrant = Math.floor(2 * angle / pi) % 4;
	const x = 50 * (1 + Math.sin(angle));
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

	const pointsStr = points.map(([x, y]) => `${x}% ${y}%`).join(" , ");
	$wr_arc.style.clipPath = `polygon(${pointsStr})`;
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

	const leftTrigger = gameData.trigger > 0.9; // TODO: tweak trigger value?
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
