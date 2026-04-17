package com.GemHunter.imagepicker;

import android.app.Activity;
import android.content.Intent;
import android.database.Cursor;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.net.Uri;
import android.provider.MediaStore;
import android.util.Base64;
import android.util.Log;
import com.unity3d.player.UnityPlayer;
import java.io.ByteArrayOutputStream;
import java.io.InputStream;

public class ImagePickerActivity extends Activity {
    private static final int PICK_IMAGE = 1;
    private static final String TAG = "ImagePickerActivity";
    private static final int TARGET_SIZE = 512;
    private static final int MAX_SIZE = 512 * 1024; // 512KB

    @Override
    protected void onCreate(android.os.Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        Log.d(TAG, "ImagePickerActivity onCreate");
        
        try {
            Intent intent = new Intent(Intent.ACTION_GET_CONTENT);
            intent.setType("image/*");
            intent.addCategory(Intent.CATEGORY_OPENABLE);
            intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            
            Log.d(TAG, "Starting image picker intent");
            startActivityForResult(Intent.createChooser(intent, "Select Picture"), PICK_IMAGE);
        } catch (Exception e) {
            Log.e(TAG, "Error in onCreate: " + e.getMessage());
            e.printStackTrace();
            finish();
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        Log.d(TAG, "onActivityResult - requestCode: " + requestCode + ", resultCode: " + resultCode);
        
        try {
            if (requestCode == PICK_IMAGE && resultCode == RESULT_OK && data != null) {
                Uri selectedImage = data.getData();
                Log.d(TAG, "Image selected: " + selectedImage.toString());
                
                // Process the image and convert to base64
                String base64Image = processImage(selectedImage);
                if (base64Image != null) {
                    UnityPlayer.UnitySendMessage("AndroidImageUploader", "OnImageSelected", base64Image);
                    Log.d(TAG, "Sent to Unity");
                } else {
                    UnityPlayer.UnitySendMessage("AndroidImageUploader", "OnImageSelected", "");
                }
            } else {
                Log.d(TAG, "No image selected or cancelled");
                UnityPlayer.UnitySendMessage("AndroidImageUploader", "OnImageSelected", "");
            }
        } catch (Exception e) {
            Log.e(TAG, "Error in onActivityResult: " + e.getMessage());
            e.printStackTrace();
            UnityPlayer.UnitySendMessage("AndroidImageUploader", "OnImageSelected", "");
        } finally {
            finish();
        }
    }

    private String processImage(Uri imageUri) {
        try {
            Log.d(TAG, "Starting image processing");
            // Load the image
            InputStream inputStream = getContentResolver().openInputStream(imageUri);
            BitmapFactory.Options options = new BitmapFactory.Options();
            options.inJustDecodeBounds = true;
            BitmapFactory.decodeStream(inputStream, null, options);
            inputStream.close();
    
            Log.d(TAG, "Original image size: " + options.outWidth + "x" + options.outHeight);
    
            // Calculate scaling
            int scale = 1;
            while ((options.outWidth * options.outHeight) * (1 / Math.pow(scale, 2)) > TARGET_SIZE * TARGET_SIZE) {
                scale++;
            }
            Log.d(TAG, "Calculated scale factor: " + scale);
    
            // Decode with scaling
            inputStream = getContentResolver().openInputStream(imageUri);
            options = new BitmapFactory.Options();
            options.inSampleSize = scale;
            Bitmap originalBitmap = BitmapFactory.decodeStream(inputStream, null, options);
            inputStream.close();
    
            if (originalBitmap == null) {
                Log.e(TAG, "Failed to decode original bitmap");
                return null;
            }
    
            Log.d(TAG, "Decoded bitmap size: " + originalBitmap.getWidth() + "x" + originalBitmap.getHeight());
    
            // Resize to exact dimensions
            Bitmap resizedBitmap = Bitmap.createScaledBitmap(originalBitmap, TARGET_SIZE, TARGET_SIZE, true);
            originalBitmap.recycle();
    
            if (resizedBitmap == null) {
                Log.e(TAG, "Failed to create scaled bitmap");
                return null;
            }
    
            Log.d(TAG, "Resized bitmap size: " + resizedBitmap.getWidth() + "x" + resizedBitmap.getHeight());
    
            // Convert to base64
            ByteArrayOutputStream byteArrayOutputStream = new ByteArrayOutputStream();
            boolean compressSuccess = resizedBitmap.compress(Bitmap.CompressFormat.PNG, 100, byteArrayOutputStream);
            resizedBitmap.recycle();
    
            if (!compressSuccess) {
                Log.e(TAG, "Failed to compress bitmap to PNG");
                return null;
            }
    
            byte[] byteArray = byteArrayOutputStream.toByteArray();
            Log.d(TAG, "Compressed PNG size: " + byteArray.length + " bytes");
    
            if (byteArray.length > MAX_SIZE) {
                Log.e(TAG, "Image too large after processing: " + byteArray.length + " bytes");
                return null;
            }
    
            String base64 = Base64.encodeToString(byteArray, Base64.NO_WRAP);
            Log.d(TAG, "Base64 string length: " + base64.length());
            return base64;
        } catch (Exception e) {
            Log.e(TAG, "Error processing image: " + e.getMessage());
            e.printStackTrace();
            return null;
        }
    }
}