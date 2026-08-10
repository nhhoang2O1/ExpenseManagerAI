import os

import numpy as np

from app.config import Settings
from app.services.paddle_ocr import PaddleOCREngine, _configure_paddle_runtime


class FakeV3Result:
    json = {
        "res": {
            "rec_texts": ["TOTAL 42.000", "TEST STORE"],
            "rec_scores": np.array([0.91, 0.97]),
            "rec_polys": np.array(
                [
                    [[0, 40], [200, 40], [200, 60], [0, 60]],
                    [[0, 0], [200, 0], [200, 20], [0, 20]],
                ]
            ),
        }
    }


class FakePaddleModel:
    def predict(self, image: np.ndarray) -> list[FakeV3Result]:
        assert image.shape == (100, 100, 3)
        return [FakeV3Result()]


def test_v3_adapter_normalizes_and_sorts_results_without_loading_model() -> None:
    engine = PaddleOCREngine(Settings())
    engine._model = FakePaddleModel()

    lines = engine.recognize(np.zeros((100, 100), dtype=np.uint8))

    assert [item.text for item in lines] == ["TEST STORE", "TOTAL 42.000"]
    assert lines[0].confidence == 0.97
    assert lines[0].box == [
        [0.0, 0.0],
        [200.0, 0.0],
        [200.0, 20.0],
        [0.0, 20.0],
    ]


def test_configure_paddle_runtime_disables_mkldnn_by_default(monkeypatch) -> None:
    monkeypatch.delenv("PADDLE_PDX_ENABLE_MKLDNN_BYDEFAULT", raising=False)

    _configure_paddle_runtime()

    assert os.environ["PADDLE_PDX_ENABLE_MKLDNN_BYDEFAULT"] == "False"


def test_document_unwarping_is_disabled_by_default() -> None:
    assert Settings().use_doc_unwarping is False
