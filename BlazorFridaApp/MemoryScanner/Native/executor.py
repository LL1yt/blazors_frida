def execute_script(session, script_code, func_name, *args):
    script = None
    try:
        script = session.create_script(script_code)
        script.load()
        api = script.exports
        result = getattr(api, func_name)(*args)
        return result
    except Exception as e:
        print(f"Error executing script: {e}")
        return None
    finally:
        if script:
            try:
                script.unload()
            except Exception:
                pass
