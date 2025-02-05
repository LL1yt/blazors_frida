import logging

logger = logging.getLogger(__name__)


async def execute_script(session, script_code, func_name, *args):
    """
    Execute a Frida script asynchronously
    Args:
        session: Frida session object
        script_code: The JavaScript code to execute
        func_name: Name of the exported function to call
        *args: Arguments to pass to the function
    Returns:
        Result of the script execution
    """
    script = None
    try:
        script = await session.create_script(script_code)
        await script.load()
        api = script.exports
        result = await getattr(api, func_name)(*args)
        return result
    except Exception as e:
        logger.error(f"Error executing script: {e}")
        raise
    finally:
        if script:
            try:
                await script.unload()
            except Exception as e:
                logger.warning(f"Error unloading script: {e}")
