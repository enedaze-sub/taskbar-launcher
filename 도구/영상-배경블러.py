"""Builds a mask video: white = launcher / launcher settings (kept sharp), black = everything else (blurred).

Foreground = pixels that differ from a clean background frame (t=0.5s, launcher hidden),
cleaned with closing + hole filling, small-blob removal, and fixed exclusions:
  * the Windows taskbar strip (always blurred)
  * the Windows file-open dialog (33.85s-41.0s; it shows personal files)

usage: python mask.py VIDEO BACKGROUND_PNG OUT_MASK_VIDEO [preview_dir t1 t2 ...]
"""
import subprocess, sys, numpy as np
from PIL import Image
from scipy import ndimage

W, H = 1918, 1076
w, h = W // 2, H // 2          # masks are computed at half resolution
FPS = 30
TASKBAR_TOP = 1031             # rows >= this are the Windows taskbar
DIALOG = (33.85, 41.0, (455, 130, 1425, 685))  # time range and x0,y0,x1,y1 of the file dialog

def half(rgb):
    return rgb.reshape(h, 2, w, 2, 3).mean(axis=(1, 3))

def mask_for(frame, bg, t):
    cur = half(frame[:h*2, :w*2])
    diff = np.abs(cur - bg).max(axis=2) > 18
    # Window drop shadows only darken what is behind them by a similar factor on every channel;
    # those pixels are background seen through a shadow, so they stay blurred.
    k = (cur * bg).sum(axis=2) / np.maximum((bg * bg).sum(axis=2), 1)   # best single darkening factor
    residual = np.abs(cur - k[..., None] * bg).max(axis=2)
    shadow = (residual < 12) & (k > 0.35) & (k < 0.985)
    diff &= ~shadow
    # close gaps along window edges (square 19px at half res), then fill enclosed holes
    closed = ndimage.minimum_filter(ndimage.maximum_filter(diff, 19), 19)
    filled = ndimage.binary_fill_holes(closed)
    # drop small blobs: cursor, clock, spinner in the background app
    labels, n = ndimage.label(filled)
    if n:
        sizes = ndimage.sum(filled, labels, range(1, n + 1))
        keep = np.zeros(n + 1, bool); keep[1:] = sizes >= 6000
        filled = keep[labels]
    filled = ndimage.binary_erosion(filled, iterations=2)  # trim any remaining shadow rim
    m = filled.astype(np.float32)
    m[TASKBAR_TOP // 2:, :] = 0
    if DIALOG[0] <= t <= DIALOG[1]:
        x0, y0, x1, y1 = (v // 2 for v in DIALOG[2]); m[y0:y1, x0:x1] = 0
    # 1px grow + soft 2px edge so the sharp/blurred boundary is not jagged
    m = ndimage.maximum_filter(m, 3)
    m = ndimage.gaussian_filter(m, 1.2)
    return (np.clip(m, 0, 1) * 255).astype(np.uint8)

def main():
    video, bg_png, out = sys.argv[1:4]
    preview = sys.argv[4:]
    bg = half(np.asarray(Image.open(bg_png).convert('RGB')).astype(np.float32)[:h*2, :w*2])
    reader = subprocess.Popen(['ffmpeg', '-v', 'error', '-i', video, '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-'], stdout=subprocess.PIPE)
    if preview:
        folder, times = preview[0], [float(x) for x in preview[1:]]
        wanted = {round(t * FPS): t for t in times}
    else:
        writer = subprocess.Popen(['ffmpeg', '-v', 'error', '-y', '-f', 'rawvideo', '-pix_fmt', 'gray', '-s', f'{w}x{h}', '-r', str(FPS), '-i', '-',
                                   '-vf', f'scale={W}:{H}:flags=bilinear', '-c:v', 'ffv1', '-pix_fmt', 'gray', out], stdin=subprocess.PIPE)
    index = 0
    while True:
        raw = reader.stdout.read(W * H * 3)
        if len(raw) < W * H * 3: break
        frame = np.frombuffer(raw, np.uint8).reshape(H, W, 3).astype(np.float32)
        t = index / FPS
        if preview:
            if index in wanted:
                m = mask_for(frame, bg, t)
                full = np.asarray(Image.fromarray(m).resize((W, H), Image.BILINEAR)).astype(np.float32)[..., None] / 255
                blurred = np.asarray(Image.fromarray(frame.astype(np.uint8)).filter(__import__('PIL.ImageFilter').ImageFilter.GaussianBlur(18))).astype(np.float32)
                comp = (frame * full + blurred * (1 - full)).astype(np.uint8)
                Image.fromarray(comp).resize((960, 538)).save(f'{folder}/test_{wanted[index]:05.1f}.png')
        else:
            writer.stdin.write(mask_for(frame, bg, t).tobytes())
        index += 1
    if not preview:
        writer.stdin.close(); writer.wait()
    print('frames', index)

if __name__ == '__main__':
    main()
