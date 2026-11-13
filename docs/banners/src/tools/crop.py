# Crops the raw session captures (CSS rect x, y, w, h at DPR 3) into src/cap/ at the stored scale.
# python3 crop.py <raw dir> <cap dir>     Only ever downscales: a stored scale above the capture's 3 is refused.
import sys, os
from PIL import Image
RAW, CAP = sys.argv[1], sys.argv[2]
DPR = 3
# name: (raw capture, (x, y, w, h) in CSS px or None for the whole frame, stored scale)
CROPS = {
  'dark-pump-scene@3x':     ('dark-desk-pump',     (250, 236, 1180, 626), 3),
  'dark-pump-card@2x':      ('dark-desk-pump',     (104, 95, 1462, 781), 2),
  'dark-ahu-card@2x':       ('dark-desk-ahu',      (104, 95, 1462, 781), 2),
  'dark-c07-card@2x':       ('dark-desk-c07',      (104, 95, 1462, 781), 2),
  'dark-inspect-panel@3x':  ('dark-desk-inspect',  (1168, 88, 418, 898), 3),
  'dark-inspect-scene@2x':  ('dark-desk-inspect',  (106, 97, 1058, 776), 2),
  'dark-sync-list@3x':      ('dark-desk-sync',     (352, 12, 978, 575), 3),
  'light-findings-work@3x': ('light-desk-findings',(216, 12, 622, 394), 3),
  'light-findings-assets@2x': ('light-desk-findings',(844, 100, 622, 446), 2),
  'dark-desk-inspect@2x':   ('dark-desk-inspect',  None, 2),
  'light-desk-inspect@2x':  ('light-desk-inspect', None, 2),
  'dark-tab-inspect@2x':    ('dark-tab-inspect',   None, 2),
  'light-tab-today@2x':     ('light-tab-today',    None, 2),
}
for name in ['dark-phone-point', 'dark-phone-today-after', 'dark-phone-findings',
             'light-phone-today', 'light-phone-point', 'light-phone-sync', 'light-phone-today-after']:
  CROPS[name + '@2x'] = (name, None, 2)
only = sys.argv[3:]
os.makedirs(CAP, exist_ok=True)
for name, (src, rect, scale) in CROPS.items():
  if only and name not in only: continue
  assert scale <= DPR, name
  im = Image.open(os.path.join(RAW, src + '.png')).convert('RGB')
  if rect:
    x, y, w, h = rect
    im = im.crop((x * DPR, y * DPR, (x + w) * DPR, (y + h) * DPR))
  if scale != DPR:
    im = im.resize((round(im.width * scale / DPR), round(im.height * scale / DPR)), Image.LANCZOS)
  im.save(os.path.join(CAP, name + '.png'), optimize=True)
  print(name, im.size)
