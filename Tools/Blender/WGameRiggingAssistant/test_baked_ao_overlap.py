import unittest
import runpy
from pathlib import Path
import numpy as np

mask=runpy.run_path(str(Path(__file__).with_name('baked_ao_overlap.py')))['overlap_mask']


class HairOverlapTests(unittest.TestCase):
    def test_adjacent_triangles_are_not_overlap(self):
        self.assertFalse(mask([[(0,0),(1,0),(1,1)],[(0,0),(1,1),(0,1)]],32,32).any())

    def test_partial_overlap_does_not_disable_whole_faces(self):
        result=mask([[(0,0),(1,0),(0,1)],[(.5,0),(1.5,0),(.5,1)]],32,32)
        self.assertTrue(result[4,20])
        self.assertFalse(result[4,4])
        self.assertFalse(result[25,25])

    def test_mirrored_duplicate_is_overlap(self):
        triangle=[(.1,.1),(.9,.1),(.1,.9)]
        self.assertTrue(mask([triangle,list(reversed(triangle))],32,32)[8,8])

    def test_many_layers_do_not_overflow(self):
        triangle=[(0,0),(1,0),(0,1)]
        np.testing.assert_array_equal(mask([triangle]*300,16,16),mask([triangle]*2,16,16))

    def test_outside_and_degenerate_triangles_are_ignored(self):
        self.assertFalse(mask([[(-3,0),(-2,0),(-3,1)],[(.5,.5)]*3],16,16).any())

    def test_nonfinite_data_is_rejected(self):
        with self.assertRaises(ValueError):mask([[(0,0),(float('nan'),1),(1,0)]],16,16)


if __name__=='__main__':unittest.main()
