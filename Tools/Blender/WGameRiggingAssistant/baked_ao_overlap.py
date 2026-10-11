"""Neutralize AO only in texels shared by multiple hair UV triangles."""
import numpy as np


def overlap_mask(triangles, width, height):
    # Four interior probes per output texel distinguish overlap from adjacent
    # triangles sharing an edge. This is texel coverage, not a whole-face mask.
    w, h = width * 2, height * 2
    coverage = np.zeros((h, w), dtype=np.uint8)
    for triangle in triangles:
        p = np.asarray(triangle, dtype=np.float64) * (w, h)
        if not np.isfinite(p).all():raise ValueError('Non-finite hair AO UV')
        a, b = p[1] - p[0], p[2] - p[0]
        determinant = a[0] * b[1] - a[1] * b[0]
        if abs(determinant) < 1e-12:continue
        low = np.maximum(0, np.floor(p.min(axis=0)).astype(int))
        high = np.minimum((w-1, h-1), np.ceil(p.max(axis=0)).astype(int))
        if np.any(high < low):continue
        y, x = np.mgrid[low[1]:high[1]+1, low[0]:high[0]+1]
        dx, dy = x + .5 - p[0, 0], y + .5 - p[0, 1]
        u = (dx*b[1] - dy*b[0]) / determinant
        v = (a[0]*dy - a[1]*dx) / determinant
        inside = (u > 1e-7) & (v > 1e-7) & (u+v < 1-1e-7)
        target = coverage[low[1]:high[1]+1, low[0]:high[0]+1]
        np.minimum(target + inside, 2, out=target)
    return (coverage.reshape(height, 2, width, 2) > 1).any(axis=(1, 3))


def neutralize_hair_overlap(meshes, rgba, size):
    triangles = []
    for obj in meshes:
        mesh = obj.data
        hair = mesh.attributes.get('WGameBakeHair')
        if hair is None:continue
        uv = mesh.uv_layers['BakeUV']
        mesh.calc_loop_triangles()
        for tri in mesh.loop_triangles:
            if hair.data[tri.polygon_index].value > .5:
                triangles.append([tuple(uv.data[i].uv) for i in tri.loops])
    mask = overlap_mask(triangles, size, size)
    values = rgba.reshape(size, size, 4)
    profile = np.minimum(15, np.floor(np.clip(values[:, :, 3], 0, 1)*16).astype(int))
    mask &= (profile == 2) | (profile == 3)
    values[mask, 0] = 1.0
    return int(mask.sum())
