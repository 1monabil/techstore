// Shared query corpus for the k6 scripts. Mix mirrors real shopper behaviour: model numbers,
// specs, brand + category, jargon/synonyms and ~10% typos.
export const BASE = __ENV.BASE_URL || 'http://localhost:5080';

export const QUERIES = [
  'rtx 4070', 'rtx4070', 'rtx 4090 laptop', 'rtx 5080', 'rx 7800 xt', 'radeon 7900 xtx', 'geforce 4060 ti',
  'asus rog strix', 'rog strix g16', 'msi katana', 'lenovo legion', 'thinkpad x1 carbon', 'macbook air m3',
  'macbook pro m4', 'dell xps 14', 'alienware m16', 'razer blade 16', 'hp omen 16', 'zenbook 14 oled',
  'iphone 16 pro', 'iphone 16 pro max 256gb', 'galaxy s25 ultra', 'pixel 9 pro', 'galaxy tab s10', 'ipad pro 13',
  'ddr5 32gb', 'ddr5 6000 cl30', '64gb ddr5', 'ddr4 16gb 3200', 'corsair vengeance', 'trident z5',
  'nvme 2tb', 'samsung 990 pro', 'sn850x 2tb', 'm.2 ssd', 'sata ssd 1tb', 'pcie 5 ssd', 'hard drive 8tb', 'nas hdd',
  'ironwolf 12tb', '27 inch 240hz', 'oled monitor', '4k monitor', 'ultrawide 34', 'qd-oled 32', 'odyssey g5',
  'mechanical keyboard', 'wireless keyboard', 'tkl keyboard', 'logitech mx keys', 'gaming mouse wireless',
  'superlight 2', 'mx master 3s', 'wireless headset', 'arctis nova', 'airpods max', 'wifi 7 router',
  'mesh wifi', 'deco x55', '8 port switch', '10g switch', 'poe switch', '850w psu', '1000w power supply',
  'rm850x', 'atx case white', 'lian li o11', 'mid tower', 'ryzen 7 9800x3d', 'ryzen 9 9950x', 'core i9 14900k',
  'core ultra 9 285k', 'am5 motherboard', 'b650 wifi', 'z790', 'x870e', 'graphics card', 'video card', 'gpu',
  // typos
  'assus rog', 'lenvo labtop', 'corsiar ram', 'nvidea rtx', 'samsng ssd', 'logitec mouse', 'keybaord', 'moniter 27',
];

export const BRANDS = ['asus', 'msi', 'gigabyte', 'lenovo', 'hp', 'dell', 'apple', 'samsung', 'corsair', 'kingston', 'logitech', 'razer'];

export const CATEGORY_FILTERS = [
  { category: 'laptops', spec: () => ({ ramGb: pick(['16', '32', '16,32']) }) },
  { category: 'laptops', spec: () => ({ gpuModel: pick(['GeForce RTX 4070 Laptop GPU', 'GeForce RTX 4060 Laptop GPU']) }) },
  { category: 'monitors', spec: () => ({ refreshRate: pick(['144', '165', '240', '165,240']) }) },
  { category: 'monitors', spec: () => ({ screenSize: pick(['27', '32', '27,32']) }) },
  { category: 'ssds', spec: () => ({ storageGb: pick(['1000', '2000', '1000,2000']), storageType: 'NVMe' }) },
  { category: 'ram', spec: () => ({ ramGb: pick(['32', '64']) }) },
  { category: 'graphics-cards', spec: () => ({ gpuBrand: pick(['NVIDIA', 'AMD']) }) },
  { category: 'components', spec: () => ({}) },
  { category: 'peripherals', spec: () => ({ connectivity: pick(['Wireless', 'Wired']) }) },
];

export const PREFIXES = [
  'ro', 'rog', 'rog s', 'rog str', 'rtx', 'rtx 4', 'rtx 40', 'rtx 407', 'sam', 'sams', 'samsung 9', 'cor', 'cors',
  'corsa', 'corsair v', 'mac', 'macb', 'macbook', 'ip', 'iph', 'iphone 1', 'log', 'logi', 'logitech g', 'ddr', 'ddr5',
  'nv', 'nvme', 'mon', 'moni', 'leno', 'lenovo l', 'leg', 'legion', 'ga', 'gam', 'gaming m', 'key', 'keyb', 'wire',
  'wireless', 'ryz', 'ryzen 7', 'core u', 'ultra', 'ast', 'asus', 'asus t', 'msi k', 'msi ka', 'dell x', 'xps',
];

export const pick = (arr) => arr[Math.floor(Math.random() * arr.length)];

export function qs(params) {
  return Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(v)}`)
    .join('&');
}
