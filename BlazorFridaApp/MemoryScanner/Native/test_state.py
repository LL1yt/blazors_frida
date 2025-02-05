#!/usr/bin/env python3
import asyncio
import logging
import uuid
from state_manager import StateManager, StateSnapshot
from scanner import MemoryScanner
from frida_module import FridaMemoryScanner

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


async def test_state_persistence():
    """Test state persistence functionality"""
    session_id = str(uuid.uuid4())
    logger.info(f"Testing state persistence with session {session_id}")

    # Initialize components
    frida_scanner = FridaMemoryScanner()
    scanner = MemoryScanner(frida_scanner, session_id)

    try:
        # Test 1: Basic state save and load
        logger.info("Test 1: Basic state operations")
        test_results = [
            {"address": 4096, "value": 42},  # 0x1000
            {"address": 8192, "value": 84},  # 0x2000
        ]
        test_metadata = {
            "value_type": "int32",
            "comparison_type": "exact",
            "ranges": [[4096, 12288]],  # [0x1000, 0x3000]
        }

        checkpoint_id = await scanner.state_manager.create_checkpoint(
            test_results, test_metadata
        )
        logger.info(f"Created checkpoint: {checkpoint_id}")

        # Load and verify state
        loaded_state = await scanner.state_manager.load_state()
        assert loaded_state is not None, "Failed to load state"
        assert loaded_state.scan_results == test_results, "Results mismatch"
        assert loaded_state.metadata == test_metadata, "Metadata mismatch"
        logger.info("Basic state operations passed")

        # Test 2: State versioning
        logger.info("Test 2: State versioning")
        updated_metadata = test_metadata.copy()
        updated_metadata["new_field"] = "test"
        await scanner.update_state({"new_field": "test"})

        new_state = await scanner.state_manager.load_state()
        assert new_state.metadata.get("new_field") == "test", "State update failed"
        logger.info("State versioning passed")

        # Test 3: Checkpoint restoration
        logger.info("Test 3: Checkpoint restoration")
        restored_state = await scanner.state_manager.restore_checkpoint(checkpoint_id)
        assert restored_state is not None, "Failed to restore checkpoint"
        assert restored_state.checkpoint_id == checkpoint_id, "Checkpoint ID mismatch"
        logger.info("Checkpoint restoration passed")

        # Test 4: Cleanup
        logger.info("Test 4: State cleanup")
        scanner.cleanup()
        logger.info("Cleanup test passed")

        logger.info("All state persistence tests passed successfully")
        return True

    except AssertionError as e:
        logger.error(f"Test failed: {str(e)}")
        return False
    except Exception as e:
        logger.error(f"Unexpected error during testing: {str(e)}")
        return False


async def main():
    success = await test_state_persistence()
    if not success:
        exit(1)


if __name__ == "__main__":
    asyncio.run(main())
